﻿using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public static class ProviderNetworkUpdate<RT>
    where RT : struct,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasAgentSyncClient<RT>,
    HasHostNetworkCommands<RT>,
    HasNetworkProviderManager<RT>,
    HasLogger<RT>
{
    public static Aff<RT, Unit> isAgentRunning() =>
        timeout(
            TimeSpan.FromSeconds(10),
            from syncClient in default(RT).AgentSync
            from ct in cancelToken<RT>()
            from isRunning in syncClient.CheckRunning(ct)
                .MapFail(_ => Error.New("The VM host agent is not running."))
            from _ in guard(isRunning,
                Error.New("The VM host agent is not running."))
            select unit);

    // ReSharper disable once InconsistentNaming
    public static Aff<RT, NetworkProvidersConfiguration> importConfig(
        string config) =>
        from parsedConfig in Eff(() => NetworkProvidersConfigYamlSerializer.Deserialize(config))
        from _ in NetworkProvidersConfigValidations.ValidateNetworkProvidersConfig(parsedConfig)
            .MapFail(issue => issue.ToError())
            .ToEff(errors => Error.New("The network provider configuration is invalid.", Error.Many(errors)))
        select parsedConfig;

    public static bool canBeAutoApplied(NetworkChanges<RT> changes) =>
        changes.Operations
            .All(o => !ProviderNetworkUpdateConstants.UnsafeChanges.Contains(o.Operation)
                      || o.Force);

    public static Aff<RT, NetworkChanges<RT>> generateChanges(
        HostState hostState,
        NetworkProvidersConfiguration newConfig,
        bool withFallback) =>

        // tools
        from changeBuilder in NetworkChangeOperationBuilder<RT>.New()
        from ovsTool in default(RT).OVS
        from hostCommands in default(RT).HostNetworkCommands

        // generate variables
        from expectedBridges in prepareExpectedBridges(newConfig)
        // The fallback data contains the host adapters also by the names
        // which have been used when the network providers have been configured.
        // This way, we can still use the last configuration even after an adapter
        // has been renamed.
        from hostStateWithFallback in withFallback
            ? addFallbackData(hostState)
            : SuccessEff(hostState)

        // Enable the OVS extension of the overlay switch(es) in case
        // it was disabled somehow. Otherwise, the execution of OVS
        // commands might later fail.
        from _1 in hostStateWithFallback.VMSwitchExtensions
            .Filter(e => e.SwitchName == EryphConstants.OverlaySwitchName && !e.Enabled)
            .Map(e => changeBuilder.EnableSwitchExtension(e.SwitchId, e.SwitchName))
            .SequenceSerial()

        // Disable the OVS extension on all switches which are not overlay switch(es).
        from _2 in hostStateWithFallback.VMSwitchExtensions
            .Filter(e => e.SwitchName != EryphConstants.OverlaySwitchName && e.Enabled)
            .Map(e => changeBuilder.DisableSwitchExtension(e.SwitchId, e.SwitchName))
            .SequenceSerial()

        // Perform a rebuild of the Hyper-V switch used for the overlay network if necessary.
        // This happens e.g. when additional physical adapters are added to the
        // network providers.
        // All OVS bridges will be removed as part of the Hyper-V switch rebuild.
        from ovsBridges1 in generateOverlaySwitchChanges(
            changeBuilder, hostStateWithFallback, expectedBridges)

        // Bridge exists in OVS but not in config -> remove it
        from ovsBridges2 in changeBuilder.RemoveUnusedBridges(
            ovsBridges1, expectedBridges)

        // Bridge exists in OVS but its adapter is missing on the host -> remove it
        // This can only happen if the bridge adapter has been manually renamed by a user.
        from ovsBridges3 in changeBuilder.RemoveBridgesWithMissingBridgeAdapter(
            expectedBridges, hostStateWithFallback.HostAdapters, ovsBridges2)

        // Add bridges from config missing in OVS
        from createdBridges in changeBuilder.AddMissingBridges(
            ovsBridges3, expectedBridges)

        from _3 in changeBuilder.UpdateBridgePorts(
            expectedBridges, createdBridges, ovsBridges3)

        // Remove NATs which are no longer needed or need to be recreated
        from validNats in changeBuilder.RemoveInvalidNats(
            hostStateWithFallback.NetNat, expectedBridges)

        // Remove ports with invalid external interfaces. This happens when:
        // - a provider is changed between overlay and NAT overlay
        // - the physical adapters of an overlay provider are changed
        from ovsBridges4 in changeBuilder.RemoveInvalidAdapterPortsFromBridges(
            expectedBridges, hostStateWithFallback.HostAdapters, ovsBridges3)

        // Configure IP settings nat_overlay adapters
        from _4 in changeBuilder.ConfigureNatAdapters(expectedBridges, createdBridges)

        // Create ports for physical adapters in overlay bridges
        from _5 in changeBuilder.ConfigureOverlayAdapterPorts(
            expectedBridges, ovsBridges4, hostStateWithFallback.HostAdapters)

        from _6 in changeBuilder.AddMissingNats(expectedBridges, validNats)

        // Update OVS bridge mapping to new network names and bridges
        from _7 in changeBuilder.UpdateBridgeMappings(expectedBridges)

        select changeBuilder.Build();

    private static Eff<Seq<NewBridge>> prepareExpectedBridges(
        NetworkProvidersConfiguration providersConfig) =>
        providersConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type is NetworkProviderType.NatOverlay or NetworkProviderType.Overlay)
            .Map(prepareNewBridgeInfo)
            .Sequence();

    private static Eff<NewBridge> prepareNewBridgeInfo(
        NetworkProvider providerConfig) =>
        from bridgeName in Optional(providerConfig.BridgeName)
            .Filter(notEmpty)
            .ToEff(Error.New($"The network provider '{providerConfig.Name}' has no bridge name."))
        from nat in providerConfig.Type is NetworkProviderType.NatOverlay
            ? prepareNewBridgeNatInfo(providerConfig).Map(Some)
            : SuccessEff(Option<NewBridgeNat>.None)
        select new NewBridge(
            bridgeName, 
            providerConfig.Name,
            providerConfig.Type,
            nat,
            providerConfig.Adapters.ToSeq(),
            Optional(providerConfig.BridgeOptions));

    private static Eff<NewBridgeNat> prepareNewBridgeNatInfo(
        NetworkProvider providerConfig) =>
        from subnet in providerConfig.Subnets.ToSeq()
            .Find(s => s.Name == "default")
            .ToEff(Error.New($"The NAT network provider '{providerConfig.Name}' has no default subnet."))
        from gateway in parseIPAddress(subnet.Gateway)
            .ToEff(Error.New($"The NAT network provider '{providerConfig.Name}' has an invalid gateway IP address."))
        from network in parseIPNetwork2(subnet.Network)
            .ToEff(Error.New($"The NAT network provider '{providerConfig.Name}' has an invalid network."))
        select new NewBridgeNat(getNetNatName(providerConfig.Name), gateway, network);

    private static string getNetNatName(string providerName)
        // The pattern for the NetNat name should be "eryph_{providerName}_{subnetName}".
        // At the moment, we only support a single provider subnet which must be named
        // 'default'. Hence, we hardcode the subnet part for now.
        => $"eryph_{providerName}_default";

    internal static Eff<HostState> addFallbackData(
        HostState hostState) =>
        from _ in unitEff
        let configuredAdapters = hostState.OvsBridges.Bridges.Values.ToSeq()
            .Bind(bridge => bridge.Ports.Values.ToSeq())
            .Bind(bridgePort => bridgePort.Interfaces)
            .Filter(interfaceInfo => interfaceInfo.IsExternal)
            .Map(interfaceInfo => from configuredName in interfaceInfo.HostInterfaceConfiguredName
                                  from interfaceId in interfaceInfo.HostInterfaceId
                                  select (interfaceId, configuredName))
            .Somes()
            .ToHashMap()
        let adapterInfos = hostState.HostAdapters.Adapters.Values.ToSeq()
            .Map(adapterInfo => adapterInfo with
            {
                ConfiguredName = configuredAdapters.Find(adapterInfo.InterfaceId)
            })
            .Map(adapterInfo => (adapterInfo.Name, AdapterInfo: adapterInfo))
        let fallbackAdapterInfos = adapterInfos
            .Map(t => from fallbackName in t.AdapterInfo.ConfiguredName
                      select t with { Name = fallbackName })
            .Somes()
        let adaptersInfoWithFallback = new HostAdaptersInfo(adapterInfos.Concat(fallbackAdapterInfos).ToHashMap())
        select hostState with { HostAdapters = adaptersInfoWithFallback };

    private static Aff<RT, OvsBridgesInfo> generateOverlaySwitchChanges(
        NetworkChangeOperationBuilder<RT> changeBuilder,
        HostState hostState,
        Seq<NewBridge> expectedBridges) =>
        from expectedOverlayAdapters in expectedBridges
            .Bind(b => b.Adapters)
            .Distinct()
            .Map(a => hostState.HostAdapters.Adapters.Find(a).ToEff($"The host adapter '{a}' does not exist."))
            .Sequence()
        let allOverlaySwitches = hostState.VMSwitches
            .Filter(s => s.Name == EryphConstants.OverlaySwitchName)
        let allOtherSwitches = hostState.VMSwitches
            .Filter(s => s.Name != EryphConstants.OverlaySwitchName)
        from _ in expectedOverlayAdapters
            .Map(a => allOtherSwitches
                .Find(s => s.NetAdapterInterfaceGuid.ToSeq().Contains(a.InterfaceId)).Match(
                    Some: s => Fail<Error, Unit>(Error.New(
                        $"The host adapter '{a.Name}' is used by the Hyper-V switch '{s.Name}'.")),
                    None: () => Success<Error, Unit>(unit)))
            .Sequence()
            .ToEff(errors => Error.New("Some host adapters are used by other Hyper-V switches.", Error.Many(errors)))
        let expectedOverlayAdapterNames = expectedOverlayAdapters.Map(a => a.Name)
        from bridges2 in expectedBridges.Length switch
        {
            > 0 => allOverlaySwitches.Match(
                Empty: () => from _ in changeBuilder.CreateOverlaySwitch(expectedOverlayAdapterNames)
                             select hostState.OvsBridges,
                Head: s => (toHashSet(s.NetAdapterInterfaceGuid.ToSeq()) == toHashSet(expectedOverlayAdapters.Map(a => a.InterfaceId))) switch
                {
                    // The OVS extension has been enabled earlier for all existing overlay switches.
                    true => SuccessAff(hostState.OvsBridges),
                    // The physical adapters of the overlay switch are not correct. We must rebuild
                    // the overlay switch with the proper adapters.
                    false => from vmAdapters in getAllVmAdapters(Seq1(s))
                             from bridges in changeBuilder.RebuildOverlaySwitch(
                                 vmAdapters, hostState.OvsBridges, expectedOverlayAdapterNames)
                             select bridges,
                },
                // Multiple overlay switches exist. We must remove all overlay switches and
                // rebuild a single overlay switch with the proper adapters.
                Tail: (h, t) => from vmAdapters in getAllVmAdapters(h.Cons(t))
                                from _ in Logger<RT>.logInformation(
                                    nameof(ProviderNetworkUpdate<RT>),
                                    "Multiple overlay switches found. The overlay switch must be completely rebuilt.")
                                from bridges in changeBuilder.RebuildOverlaySwitch(
                                    vmAdapters, hostState.OvsBridges, expectedOverlayAdapterNames)
                                select bridges),
            _ => allOverlaySwitches.Match(
                Empty: () => SuccessAff(hostState.OvsBridges),
                Seq: s => from vmAdapters in getAllVmAdapters(s)
                          from ovsBridges in changeBuilder.RemoveOverlaySwitch(vmAdapters, hostState.OvsBridges)
                          select ovsBridges),
        }
        select bridges2;

    private static Aff<RT, Seq<TypedPsObject<VMNetworkAdapter>>> getAllVmAdapters(
        Seq<VMSwitch> switches) => 
        from hostCommands in default(RT).HostNetworkCommands
        from vmAdapters in switches
            .Map(s => hostCommands.GetNetAdaptersBySwitch(s.Id))
            .SequenceSerial()
        select vmAdapters.Flatten();

    private record OperationError([property: DataMember] Seq<NetworkChangeOperation<RT>> ExecutedOperations, Error Cause)
        : Expected("Operation failed", 100, Cause);


    private static Aff<RT, Unit> autoRollbackChanges(Error error)
    {
        if (error is not OperationError opError)
            return FailAff<Unit>(error);

        var executedOpCodes = opError.ExecutedOperations
            .Select(o => o.Operation);
        return opError.ExecutedOperations.Where(o => (
                o.CanRollBack?.Invoke(executedOpCodes)).GetValueOrDefault())
            .Where(o => o.Rollback != null)
            .Map(o => o.Rollback!()).TraverseSerial(l => l)
            .Bind(_ => FailAff<Unit>(opError.Cause));
    }

    public static Aff<RT, Unit> executeChanges(NetworkChanges<RT> changes)
    {
        Seq<NetworkChangeOperation<RT>> executedOps = default;

        if (changes.Operations.Length == 0)
            return unitAff;

        return
            (from l1 in Logger<RT>.logInformation<NetworkChanges<RT>>("Executing network changes: {changes}", changes.Operations.Select(x=>x.Operation))
             from ops in changes.Operations.Map(o =>
                    from _ in o.Change().Map(
                        r =>
                        {
                            executedOps = executedOps.Add(o);
                            return r;
                        }
                    )
                    select unit).TraverseSerial(l => l)
                select unit)

            .Bind(_ => Logger<RT>.logInformation<NetworkChanges<RT>>("Network changes successfully applied."))
            .Map(_ => unit)
            .MapFail(e => new OperationError(executedOps, e))
            .IfFailAff(autoRollbackChanges);
    }
}
