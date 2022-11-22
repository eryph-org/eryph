using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Settings;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;
using Array = System.Array;

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

    private static Aff<RT, Unit> isAgentRunning()
    {
        var cts = new CancellationTokenSource(2000);

        return default(RT).AgentSync.Bind(a => a.CheckRunning(cts.Token).Bind(r =>
                r ? SuccessAff(Unit.Default) : FailAff<Unit>(Error.New("")))
            .MapFail(_ => Error.FromObject("VM Host Agent is not running")));
    }

    // ReSharper disable once InconsistentNaming
    public static Aff<RT, NetworkProvidersConfiguration> importConfig(string config)

    {
        return from inputConfig in
                NetworkProviderManager<RT>.parseConfigurationYaml(config)
            from newConfig in validateConfiguration(inputConfig)
            select newConfig;

    }

    private static Aff<RT, NetworkProvidersConfiguration> validateConfiguration(NetworkProvidersConfiguration config)
    {
        if (config.NetworkProviders == null || config.NetworkProviders.Length == 0)
            return LanguageExt.Aff<NetworkProvidersConfiguration>.Fail("Provider configuration is empty");

        return config.NetworkProviders.Map(NetworkProvider.Validate)

            .Traverse(l => l)
            .ToAff(errors => Error.New(errors.Aggregate((allErrors, error) => allErrors + "\n" + error)))
            .Bind(_ => isAgentRunning())
            .Map(_ => config);

    }

    private static readonly NetworkChangeOperation[] UnsafeChanges = new[]
    {
        NetworkChangeOperation.RemoveAdapterPort,
        NetworkChangeOperation.AddAdapterPort,
        NetworkChangeOperation.RebuildOverLaySwitch,
    };

    public static bool canBeAutoApplied(NetworkChanges<RT> changes) =>
       ! changes.Operations.Select(x => x.Operation)
            .Any(x => UnsafeChanges.Contains(x));
            

    public static Aff<RT, NetworkChanges<RT>> generateChanges(
        HostState hostState,
        NetworkProvidersConfiguration newConfig) =>

        // tools
        from changeBuilder in NetworkChangeOperationBuilder<RT>.New()
        from ovsTool in default(RT).OVS
        from hostCommands in default(RT).HostNetworkCommands

        // generate variables
        let currentOvsBridges = new OVSBridgeInfo(
            new Lst<string>(hostState.OVSBridges.Select(x => x.Name)),
            hostState.OvsBridgePorts.Map(port => (port.Name, hostState.OVSBridges.First(b =>
                    b.Ports.Contains(port.Id)).Name))
                .ToHashMap())
        let newBridges = newConfig.NetworkProviders
            .Where(x => x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay)
            .Select(x => new NewBridge(x.BridgeName,
                x.Type == NetworkProviderType.NatOverLay
                    ? IPAddress.Parse(x.Subnets.First(s => s.Name == "default").Gateway)
                    : IPAddress.None,
                IPNetwork.Parse(x.Subnets.First(s => s.Name == "default").Network)))
            .ToSeq()
        let newOverlayAdapters = toHashSet(newConfig.NetworkProviders
            .Where(x => x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay)
            .Where(x => x.Adapters != null)
            .SelectMany(x => x.Adapters).Distinct())

        let enabledBridges = (newConfig.EnabledBridges?? Array.Empty<string>())
            .AsEnumerable()
            .Append(newConfig.NetworkProviders
            .Where(x=>x.Type == NetworkProviderType.NatOverLay)
            .Select(x=>x.BridgeName))
            .ToSeq()
                                    
        let needsOverlaySwitch = newBridges.Length > 0
        let hasOverlaySwitch = hostState.OverlaySwitch.IsSome

        // bridge exists in OVS but not in config -> remove it
        from pendingBridges in changeBuilder
            .RemoveUnusedBridges(currentOvsBridges, newBridges)

        // create, delete or rebuild overlay switch
        from ovsBridges2 in
            !hasOverlaySwitch
                ? needsOverlaySwitch
                    ? // no switch, but needs one
                    changeBuilder.CreateOverlaySwitch(newOverlayAdapters.ToSeq())
                        .Map(_ => pendingBridges)
                    : // no switch needed
                    SuccessAff(pendingBridges)
                : // has switch
                from switchInfo in hostState.OverlaySwitch.ToAff(Error.New("switch should exist"))
                from overlayVMAdapters in hostCommands
                    .GetNetAdaptersBySwitch(switchInfo.Id)
                from bridgeChange in !needsOverlaySwitch
                    ? // no switch needed
                    changeBuilder.RemoveOverlaySwitch(
                        overlayVMAdapters, pendingBridges)
                    : // switch needs rebuild
                    switchInfo.AdaptersInSwitch != newOverlayAdapters
                        ? changeBuilder.RebuildOverlaySwitch(
                            overlayVMAdapters, pendingBridges, newOverlayAdapters)
                        : SuccessAff(pendingBridges)
                select bridgeChange

        // remove missing NAT bridges (renamed?)
        from ovsBridges3 in changeBuilder.RecreateMissingNatAdapters(
            newConfig, hostState.AllNetAdaptersNames, ovsBridges2)

            // add bridges from config missing in OVS
        from createdBridges in changeBuilder.AddMissingBridges(
            hasOverlaySwitch, enabledBridges, ovsBridges3, newBridges)

        // remove no longer needed network nat(s) 
        from uRemoveNat in changeBuilder.RemoveUnusedNat(
            hostState.NetNat, newConfig, newBridges)

        // remove any adapter on nat overlays (happens if type is changed to nat_overlay)
        from ovsBridges4 in changeBuilder.RemoveAdapterPortsOnNatOverlays(
            newConfig, hostState.NetAdapters, ovsBridges3)

        // configure ip settings and nat for nat_overlay adapters
        from uNatAdapter in changeBuilder.ConfigureNatAdapters(
            newConfig, hostState.NetNat, createdBridges, newBridges)

        // create ports for adapters in overlay bridges
        from uCreatePorts in changeBuilder.CreateOverlayAdapterPorts(
            newConfig, ovsBridges4)

        // update OVS bridge mapping to new network names and brdiges
        from uBrideMappings in changeBuilder.UpdateBridgeMappings(
            newConfig)
        select changeBuilder.Build();

    public static Aff<RT, HostState> getHostState() =>
        getHostState(() => unitEff);

    public static Aff<RT, HostState> getHostState(Func<Eff<RT, Unit>> progressCallback) =>
        // tools
        from ovsTool in default(RT).OVS
        from hostCommands in default(RT).HostNetworkCommands

        // collect network state of host
        from p1 in progressCallback()
        from vmSwitchExtensions in hostCommands.GetSwitchExtensions()
        from p2 in progressCallback()
        from vmSwitches in hostCommands.GetSwitches()
        from p3 in progressCallback()
        from netAdapters in hostCommands.GetPhysicalAdapters()
        from p4 in progressCallback()
        from allAdapterNames in hostCommands.GetAdapterNames()
        from p5 in progressCallback()
        from overlaySwitch in hostCommands.FindOverlaySwitch(vmSwitches, vmSwitchExtensions, netAdapters)
        from p6 in progressCallback()
        from netNat in hostCommands.GetNetNat()
        from p7 in progressCallback()
        from ovsBridges in ovsTool.GetBridges().ToAff(l => l)
        from p8 in progressCallback()
        from ovsBridgePorts in ovsTool.GetPorts().ToAff(l => l)
        let hostState = new HostState(vmSwitchExtensions, vmSwitches, netAdapters, allAdapterNames, overlaySwitch, netNat, ovsBridges,
            ovsBridgePorts)
        from ls in Logger<RT>.logTrace<HostState>("fetched host state: {hostState}", hostState)
        select hostState;


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