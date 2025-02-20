using System;
using System.Linq;
using System.Net;
using System.Threading;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public class NetworkChangeOperationBuilder<RT> where RT : struct,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasHostNetworkCommands<RT>,
    HasAgentSyncClient<RT>,
    HasLogger<RT>
{
    private Seq<NetworkChangeOperation<RT>> _operations;

    internal static Aff<RT, NetworkChangeOperationBuilder<RT>> New() =>
        SuccessAff(new NetworkChangeOperationBuilder<RT>());

    public NetworkChanges<RT> Build()
    {
        return new NetworkChanges<RT>() { Operations = _operations };
    }

    public Aff<RT, Unit> CreateOverlaySwitch(Seq<string> adapters) =>
        from _1 in LogTrace("Entering {Method}", nameof(CreateOverlaySwitch))
        from _2 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.CreateOverlaySwitch(adapters)
                  select unit,
            NetworkChangeOperation.CreateOverlaySwitch)
        from _3 in AddOperation(
            StartOVN,
            NetworkChangeOperation.StartOVN)
        select unit;

    public Aff<RT, OvsBridgesInfo> RebuildOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo ovsBridges,
        Seq<string> newOverlayAdapters) =>
        from _1 in LogTrace("Entering {Method}", nameof(RebuildOverlaySwitch))
        from _2 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: a => from _1 in LogDebug("Found adapters on overlay switch. Adding disconnect and reconnect operations.")
                      from _2 in DisconnectOverlayVmAdapters(a)
                      select unit)
        from _3 in AddOperation(
            StopOVN,
            _ => true,
            StartOVN,
            NetworkChangeOperation.StopOVN)
        from _4 in ovsBridges.Bridges.Keys
            .Map(AddRemoveBridgeOperation)
            .SequenceSerial()
        from _5 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.RemoveOverlaySwitch()
                  from _2 in hostCommands.CreateOverlaySwitch(newOverlayAdapters)
                  select unit,
            NetworkChangeOperation.RebuildOverLaySwitch)
        from _6 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: ConnectOverlayVmAdapters)
        // When OVN is started, it automatically creates the integration bridge br-int.
        from _7 in AddOperation(
            StartOVN,
            NetworkChangeOperation.StartOVN)
        select default(OvsBridgesInfo);

    private Aff<RT, Unit> DisconnectOverlayVmAdapters(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters) =>
        AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.DisconnectNetworkAdapters(overlayVMAdapters)
                  select unit,
            _ => true,
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.ReconnectNetworkAdapters(overlayVMAdapters)
                  select unit,
            NetworkChangeOperation.DisconnectVMAdapters);

    private Aff<RT, Unit> ConnectOverlayVmAdapters(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters) =>
        AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.ConnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName)
                  select unit,
            NetworkChangeOperation.ConnectVMAdapters);

    private Aff<RT, Unit> AddRemoveBridgeOperation(string bridgeName) =>
        from _1 in LogDebug("Adding operation to remove bridge {bridge}", bridgeName)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS.ToAff()
                from ct in cancelToken<RT>()
                from _ in ovs.RemoveBridge(bridgeName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.RemoveBridge, 
            bridgeName)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo currentBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(RemoveOverlaySwitch))
        from _2 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: a => from _1 in LogDebug("Found adapters on overlay switch. Adding disconnect operations.")
                      from _2 in DisconnectOverlayVmAdapters(a)
                      select unit)
        from _3 in AddOperation(
            StopOVN,
            _ => true,
            StartOVN,
            NetworkChangeOperation.StopOVN)
        from _4 in currentBridges.Bridges.Keys
            .Map(AddRemoveBridgeOperation)
            .SequenceSerial()
        from _5 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.RemoveOverlaySwitch()
                  select unit,
            NetworkChangeOperation.RemoveOverlaySwitch)
        select default(OvsBridgesInfo);
    
    public Aff<RT, Unit> EnableSwitchExtension(
        Guid switchId,
        string switchName) =>
        from _1 in LogTrace("Entering {Method}", nameof(EnableSwitchExtension))
        from _2 in AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                      from _ in hostCommands.EnableSwitchExtension(switchId)
                      select unit,
                NetworkChangeOperation.EnableSwitchExtension,
                switchName)
        select unit;

    public Aff<RT, Unit> DisableSwitchExtension(
        Guid switchId,
        string switchName) =>
        from _1 in LogTrace("Entering {Method}", nameof(DisableSwitchExtension))
        from _2 in AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                      from _ in hostCommands.DisableSwitchExtension(switchId)
                      select unit,
                NetworkChangeOperation.DisableSwitchExtension,
                switchName)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveUnusedBridges(
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> newBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(RemoveUnusedBridges))
        let newBridgeNames = toHashSet(newBridges.Map(b => b.BridgeName))
        let unusedBridges = ovsBridges.Bridges.Values
            .Filter(b => b.Name != "br-int")
            .Filter(b => !newBridgeNames.Contains(b.Name))
            .Map(b => b.Name)
        from _2 in unusedBridges
            .Map(RemoveUnusedBridge)
            .SequenceSerial()
        let result = unusedBridges
            .Fold(ovsBridges, (s, b) => s.RemoveBridge(b))
        select result;

    private Aff<RT, Unit> RemoveUnusedBridge(
        string bridgeName) =>
        from _1 in LogDebug("Adding operation to remove bridge {bridge}", bridgeName)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS.ToAff()
                from ct in cancelToken<RT>()
                from _ in ovs.RemoveBridge(bridgeName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.RemoveUnusedBridge,
            bridgeName)
        select unit;

    private (Option<int> VlanTag, Option<string> VlanMode) GetBridgePortSettings(
        Option<NetworkProviderBridgeOptions> options) =>
        (
            options.Bind(o => Optional(o.BridgeVlan)),
            options.Bind(o => o.VlanMode switch
            {
                BridgeVlanMode.Access => Some("access"),
                BridgeVlanMode.NativeUntagged => Some("native-untagged"),
                BridgeVlanMode.NativeTagged => Some("native-tagged"),
                _ => None
            })
        );

    public Aff<RT, Seq<string>> AddMissingBridges(
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> newBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(AddMissingBridges))
        let missingBridges = newBridges
            .Filter(b => !ovsBridges.Bridges.Find(b.BridgeName).IsSome)
        from _2 in missingBridges
            .Map(AddMissingBridge)
            .SequenceSerial()
        let missingBridgeNames = missingBridges.Map(b => b.BridgeName)
        select missingBridgeNames;

    private Aff<RT, Unit> AddMissingBridge(NewBridge newBridge) =>
        from _1 in LogDebug("Adding operation to add bridge {bridge}", newBridge.BridgeName)
        let portSettings = GetBridgePortSettings(newBridge.Options)
        let defaultIpMode = newBridge.Options
            .Bind(o => Optional(o.DefaultIpMode))
            .IfNone(BridgeHostIpMode.Disabled)
        let enableBridge = defaultIpMode != BridgeHostIpMode.Disabled
        from _2 in AddOperation(
            () => from c in default(RT).HostNetworkCommands
                from ovs in default(RT).OVS
                from _1 in timeout(
                    TimeSpan.FromSeconds(30),
                    from ct in cancelToken<RT>()
                    from _ in ovs.AddBridge(newBridge.BridgeName, ct).ToAff(e => e)
                    select unit)
                from _2 in timeout(
                    TimeSpan.FromSeconds(30),
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgePort(newBridge.BridgeName, portSettings.VlanTag, portSettings.VlanMode, ct).ToAff(e => e)
                    select unit)
                from _3 in c.WaitForBridgeAdapter(newBridge.BridgeName)
                from _4 in enableBridge ? c.EnableBridgeAdapter(newBridge.BridgeName) : unitAff
                select unit,
            NetworkChangeOperation.AddBridge,
            newBridge.BridgeName)
        select unit;

    public Aff<RT, Seq<NetNat>> RemoveInvalidNats(
        Seq<NetNat> netNats,
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(RemoveInvalidNats))
        from removedNats in netNats.Filter(n => n.Name.StartsWith("eryph_"))
            .Map(n => RemoveInvalidNat(n, newConfig, newBridges))
            .SequenceSerial()
        select netNats.Except(removedNats.Somes()).ToSeq();

    private Aff<RT, Option<NetNat>> RemoveInvalidNat(
        NetNat nat,
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges) =>
        from _ in unitAff
        let providerConfig = newConfig.NetworkProviders.ToSeq()
            .Find(p => GetNetNatName(p.Name) == nat.Name && p.Type == NetworkProviderType.NatOverlay)
        // When the prefix of the NetNat is invalid, we will just recreate the NetNat.
        let natPrefix = parseIPNetwork2(nat.InternalIPInterfaceAddressPrefix)
        let bridge = providerConfig.Bind(p => newBridges.Find(b => b.BridgeName == p.BridgeName))
        let isNatValid = bridge.Map(b => b.Network == natPrefix).IfNone(false)
        from result in isNatValid
            ? SuccessAff(Option<NetNat>.None)
            : from _1 in LogDebug("Removing invalid host NAT '{Nat}'", nat.Name)
              from _2 in AddOperation(
                  () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                        from _ in hostCommands.RemoveNetNat(nat.Name)
                        select unit,
                  NetworkChangeOperation.RemoveNetNat,
                  nat.Name)
              select Some(nat)
        select result;

    public Aff<RT, Unit> AddMissingNats(
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges,
        Seq<NetNat> netNats) =>
        from _1 in unitAff
        let unmanagedNats = netNats
            .Filter(n => !n.Name.StartsWith("eryph_"))
            .Map(n => from network in parseIPNetwork2(n.InternalIPInterfaceAddressPrefix)
                      select (n.Name, Network: network))
            .Somes()
        from _2 in newConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type == NetworkProviderType.NatOverlay)
            // Any invalid NATs would have been removed earlier. When the NAT
            // still exists at this point, it is valid.
            .Filter(np => !netNats.Exists(n => n.Name == GetNetNatName(np.Name)))
            .Map(np => AddMissingNat(np, newBridges, unmanagedNats))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> AddMissingNat(
        NetworkProvider providerConfig,
        Seq<NewBridge> bridges,
        Seq<(string Name, IPNetwork2 Network)> unmanagedNats) =>
        from bridge in bridges.Find(b => b.BridgeName == providerConfig.BridgeName)
            .ToAff(Error.New($"BUG! Info for configured bridge '{providerConfig.BridgeName}' is missing."))
        from _1 in unmanagedNats.Find(un => un.Network.Overlap(bridge.Network)).Match(
            Some: un => FailEff<Unit>(Error.New(
                $"The IP range '{bridge.Network}' of the provider '{providerConfig.Name}' overlaps "
                    + $"the IP range '{un.Network}' of the NAT '{un.Name}' which is not managed by eryph.")),
            None: () => unitEff)
        let natName = GetNetNatName(providerConfig.Name)
        from _2 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.AddNetNat(natName, bridge.Network)
                  select unit,
            _ => true,
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.RemoveNetNat(natName)
                  select unit,
            NetworkChangeOperation.AddNetNat,
            natName, bridge.Network)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveInvalidAdapterPortsFromBridges(
        NetworkProvidersConfiguration newConfig,
        HostAdaptersInfo adaptersInfo,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(RemoveInvalidAdapterPortsFromBridges))
        from changedBridges in newConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay)
            .Map(np => from ovsBridge in ovsBridges.Bridges.Find(np.BridgeName)
                       select (ProviderConfig: np, OvsBridge: ovsBridge))
            .Somes()
            .Map(r => RemoveInvalidAdapterPortsFromBridge(r.ProviderConfig, r.OvsBridge, adaptersInfo))
            .SequenceSerial()
        let result = changedBridges.Fold(ovsBridges, (s, b) => s.SetBridge(b))
        select result;

    public Aff<RT, OvsBridgeInfo> RemoveInvalidAdapterPortsFromBridge(
        NetworkProvider providerConfig,
        OvsBridgeInfo ovsBridge,
        HostAdaptersInfo adaptersInfo) =>
        from _1 in unitAff
        let portsWithPhysicalInterfaces = ovsBridge.Ports.Values.ToSeq()
            .Filter(p => p.Interfaces.Exists(i => i.IsExternal))
        // There must always be at most one port with external interfaces.
        // When multiple external interfaces should be used, the port should be bonded.
        from arePortsValid in portsWithPhysicalInterfaces.Match(
            Empty: () => SuccessEff(true),
            Head: port => IsAdapterPortValid(providerConfig, port, adaptersInfo),
            Tail: _ => SuccessEff(false))
        let physicalAdapterNames = adaptersInfo.Adapters.Values.ToSeq()
            .Filter(a => a.IsPhysical)
            .Map(a => a.Name)
        from result in arePortsValid switch
        {
            true => SuccessAff(ovsBridge),
            false => from _ in portsWithPhysicalInterfaces
                        .Map(RemoveInvalidAdapterPortFromBridge)
                        .SequenceSerial()
                    select ovsBridge.RemovePorts(portsWithPhysicalInterfaces.Map(p => p.PortName)),
        }
        select result;

    private Eff<RT, bool> IsAdapterPortValid(
        NetworkProvider providerConfig,
        OvsBridgePortInfo port,
        HostAdaptersInfo adaptersInfo) =>
        from expectedAdapterInfos in providerConfig.Adapters.ToSeq()
            .Map(a => adaptersInfo.Adapters.Find(a)
                .ToEff(Error.New($"The adapter '{a}' of the provider '{providerConfig.Name}' does not exist.")))
            .Sequence()
        let expectedAdapters = toHashSet(expectedAdapterInfos.Map(a => (a.Name, HostInterfaceId: Some(a.InterfaceId))))
        let actualAdapters = toHashSet(port.Interfaces.Map(i => (i.Name, i.HostInterfaceId)))
        select expectedAdapters == actualAdapters;

    private Aff<RT, Unit> RemoveInvalidAdapterPortFromBridge(
        OvsBridgePortInfo port) =>
        from _1 in LogDebug("The port {PortName} of bridge {BridgeName} uses unnecessary or invalid external interfaces. Removing the port.",
            port.PortName, port.BridgeName)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS.ToAff()
                from ct in cancelToken<RT>().ToAff()
                from _ in ovs.RemovePort(port.BridgeName, port.PortName, ct).ToAff(l => l)
                select unit),
            NetworkChangeOperation.RemoveAdapterPort,
            port.PortName, port.BridgeName)
        select unit;

    public Aff<RT, Unit> ConfigureNatAdapters(
        NetworkProvidersConfiguration newConfig,
        Seq<NetNat> netNat,
        Seq<string> createdBridges,
        Seq<NewBridge> newBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(ConfigureNatAdapters))
        from _2 in newConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type is NetworkProviderType.NatOverlay)
            .Map(np => ConfigureNatAdapter(np, netNat, createdBridges, newBridges))
            .SequenceSerial()
        select unit;
    
    private Aff<RT, Unit> ConfigureNatAdapter(
        NetworkProvider providerConfig,
        Seq<NetNat> netNat,
        Seq<string> createdBridges,
        Seq<NewBridge> newBridges) =>
        from newBridge in newBridges.Find(x => x.BridgeName == providerConfig.BridgeName)
            .ToAff(Error.New($"Could not find bridge '{providerConfig.BridgeName}' which should have been created."))
        let isNewCreatedBridge = createdBridges.Contains(newBridge.BridgeName)
        let newNatName = GetNetNatName(providerConfig.Name)
        from isBridgeAdapterIpValid in isNewCreatedBridge
            ? SuccessAff(false)
            : IsBridgeAdapterIpValid(newBridge)
        from _1 in isBridgeAdapterIpValid
            ? unitAff   
            : AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                    from _1 in hostCommands.EnableBridgeAdapter(newBridge.BridgeName)
                    from _2 in hostCommands.ConfigureAdapterIp(newBridge.BridgeName, newBridge.IPAddress,
                        newBridge.Network)
                    select unit,
                NetworkChangeOperation.ConfigureNatIp,
                newBridge.BridgeName)
        select unit;

    private Aff<RT, bool> IsBridgeAdapterIpValid(NewBridge bridge) =>
        from hostCommands in default(RT).HostNetworkCommands.ToAff()
        from ipAddresses in hostCommands.GetAdapterIpV4Address(bridge.BridgeName)
        from isValid in ipAddresses.Match(
            Empty: () => SuccessAff(false),
            Head: ip => from _1 in unitAff
                        let isMatch = ip.PrefixLength == bridge.Network.Cidr
                                      && ip.IPAddress == bridge.IPAddress.ToString()
                        from _2 in isMatch
                            ? unitAff
                            : LogDebug("Host nat adapter {bridgeName} has invalid ip. Expected: {expectedIp}/{expectedSuffix}, Actual: {actualIp}/{actualSuffix}",
                                bridge.BridgeName, bridge.IPAddress, bridge.Network.Cidr, ip.IPAddress, ip.PrefixLength)
                        select isMatch,
            Tail: _ => SuccessAff(false))
        select isValid;
    
    
    /// <summary>
    /// This method removes OVS bridges for which the host network adapter no longer exist.
    /// The bridges will be recreated later.
    /// </summary>
    public Aff<RT, OvsBridgesInfo> RemoveBridgesWithMissingBridgeAdapter(
        NetworkProvidersConfiguration newConfig,
        HostAdaptersInfo hostAdaptersInfo,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(RemoveBridgesWithMissingBridgeAdapter))
        let bridgesToRemove = newConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay)
            .Filter(np => ovsBridges.Bridges.Find(np.BridgeName).IsSome)
            .Filter(np => hostAdaptersInfo.Adapters.Find(np.BridgeName).Filter(a => !a.IsPhysical).IsNone)
            .Map(np => np.BridgeName)
        from _2 in bridgesToRemove
            .Map(bridgeName =>
                from _1 in LogDebug("Host adapter for bridge '{Bridge}' not found. Recreating bridge.", bridgeName)
                from _2 in AddOperation(
                    () => timeout(
                        TimeSpan.FromSeconds(30),
                        from ovs in default(RT).OVS.ToAff()
                        from ct in cancelToken<RT>().ToAff()
                        from _ in ovs.RemoveBridge(bridgeName, ct).ToAff(e => e)
                        select unit),
                    NetworkChangeOperation.RemoveMissingBridge,
                    bridgeName)
                select unit)
            .SequenceSerial()
        let result = bridgesToRemove.Fold(ovsBridges, (s, b) => s.RemoveBridge(b))
        select result;

    public Aff<RT, Unit> UpdateBridgePorts(
        NetworkProvidersConfiguration newConfig,
        Seq<string> createdBridges,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogTrace("Entering {Method}", nameof(UpdateBridgePorts))
        from _2 in newConfig.NetworkProviders.ToSeq()
            .Filter(np => np.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay)
            .Filter(np => !createdBridges.Contains(np.BridgeName))
            .Map(np => UpdateBridgePort(np, ovsBridges))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> UpdateBridgePort(
        NetworkProvider providerConfig,
        OvsBridgesInfo ovsBridgesInfo) =>
        from ovsBridgeInfo in ovsBridgesInfo.Bridges.Find(providerConfig.BridgeName)
            .ToAff(Error.New($"BUG! Bridge '{providerConfig.BridgeName}' is missing during bridge port update."))
        from ovsBridgePort in ovsBridgeInfo.Ports.Find(providerConfig.BridgeName)
            .ToAff(Error.New($"BUG! Bridge port '{providerConfig.BridgeName}' is missing during bridge port update."))
        let expectedPortSettings = GetBridgePortSettings(providerConfig.BridgeOptions)
        let currentPortSettings = (ovsBridgePort.Tag, ovsBridgePort.VlanMode)
        from _ in expectedPortSettings == currentPortSettings
            ? unitAff
            : AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgePort(providerConfig.BridgeName, expectedPortSettings.VlanTag, expectedPortSettings.VlanMode, ct)
                            .ToAff(e => e)
                    select unit),
                _ => true,
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgePort(providerConfig.BridgeName, currentPortSettings.Tag , currentPortSettings.VlanMode, ct)
                        .ToAff(e => e)
                    select unit), 
                NetworkChangeOperation.UpdateBridgePort, providerConfig.BridgeName)
        select unit;

    public Aff<RT, Unit> ConfigureOverlayAdapterPorts(
        NetworkProvidersConfiguration newConfig,
        OvsBridgesInfo ovsBridges,
        HostAdaptersInfo hostAdaptersInfo) =>
        from _1 in LogTrace("Entering {Method}", nameof(ConfigureOverlayAdapterPorts))
        from _2 in newConfig.NetworkProviders.ToSeq()
            .Filter(x => x.Type is NetworkProviderType.Overlay)
            .Filter(x => x.Adapters is { Length: > 0 })
            .Map(np => ConfigureOverlayAdapterPort(np, ovsBridges, hostAdaptersInfo))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> ConfigureOverlayAdapterPort(
        NetworkProvider providerConfig,
        OvsBridgesInfo ovsBridges,
        HostAdaptersInfo hostAdaptersInfo) =>
        from _1 in unitAff
        from expectedPortName in providerConfig.Adapters.ToSeq().Match(
            Empty: () => FailEff<string>(Error.New("BUG! No adapters configured when trying to add overlay adapters.")),
            Head: SuccessEff,
            Tail: _ => SuccessEff(GetBondPortName(providerConfig.BridgeName!)))
        let expectedAdapters = toHashSet(providerConfig.Adapters)  
        // When the port uses the wrong adapters, we already removed the port earlier.
        // Hence, if the port exists, the adapters are correct, and we only need to check the bond setting.
        let existingPort = ovsBridges.Bridges
            .Find(providerConfig.BridgeName)
            .Bind(b => b.Ports.Find(expectedPortName))
            .Filter(p => toHashSet(p.Interfaces.Map(i => i.Name)) == expectedAdapters)
        // When the provider has no adapters configured at all, we do not need to create
        // or update the overlay port.
        from _4 in expectedAdapters.Count > 0
            ? existingPort.Match(
                None: () => AddOverlayAdapterPort(providerConfig, expectedPortName, hostAdaptersInfo),
                Some: portInfo => UpdateOverlayAdapterPort(providerConfig, portInfo))
            : unitAff
        select unit;
    
    private Aff<RT, Unit> AddOverlayAdapterPort(
        NetworkProvider providerConfig,
        string expectedPortName,
        HostAdaptersInfo hostAdaptersInfo) =>
        from _1 in unitAff
        let adapters = providerConfig.Adapters.ToSeq().Strict()
        from expectedAdapters in providerConfig.Adapters.ToSeq()
            .Map(adapterName => FindHostAdapter(hostAdaptersInfo, adapterName, providerConfig.BridgeName))
            .Sequence()
        from _2 in expectedAdapters.Match(
                Empty: () => unitAff,
                Head: a => AddSimpleOverlayAdapterPort(providerConfig, expectedPortName, a),
                Tail: (h, t) => AddBondedOverlayAdapterPort(providerConfig, expectedPortName, h.Cons(t)))
        select unit;

    private Eff<HostAdapterInfo> FindHostAdapter(
        HostAdaptersInfo adaptersInfo,
        string adapterName,
        string bridgeName) =>
        from adapterInfo in adaptersInfo.Adapters.Find(adapterName)
            .ToEff(Error.New($"Could not find the host adapter '{adapterName}' for bridge '{bridgeName}'."))
        from _1 in guard(adapterInfo.IsPhysical,
                Error.New($"The host adapter '{adapterName}' for bridge '{bridgeName}' is not a physical adapter."))
            .ToEff()
        select adapterInfo;

    private Aff<RT, Unit> AddBondedOverlayAdapterPort(
        NetworkProvider providerConfig,
        string expectedPortName,
        Seq<HostAdapterInfo> adapters) =>
        from _1 in unitAff
        let interfaces = adapters.Map(a => new InterfaceUpdate(a.Name, a.InterfaceId, a.Name))
        let ovsBondMode = GetBondMode(providerConfig.BridgeOptions?.BondMode)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.AddBond(providerConfig.BridgeName!, expectedPortName, interfaces, ovsBondMode, ct).ToAff(e => e)
                select unit),
            _ => true,
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(providerConfig.BridgeName!, expectedPortName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.AddBondPort,
            expectedPortName, string.Join(", ", adapters.Map(a => a.Name)), providerConfig.BridgeName!)
        select unit;

    private Aff<RT, Unit> AddSimpleOverlayAdapterPort(
        NetworkProvider providerConfig,
        string expectedPortName,
        HostAdapterInfo adapter) =>
        from _1 in guard(expectedPortName == adapter.Name,
                Error.New($"BUG! Mismatch of port name {expectedPortName} and adapter name {adapter.Name}."))
            .ToAff()
        let @interface = new InterfaceUpdate(adapter.Name, adapter.InterfaceId, adapter.Name)
        // TODO look 
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.AddPort(providerConfig.BridgeName!, @interface, ct).ToAff(e => e)
                select unit),
            _ => true,
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(providerConfig.BridgeName!, expectedPortName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.AddAdapterPort,
            expectedPortName, providerConfig.BridgeName!)
        select unit;

    private Aff<RT, Unit> UpdateOverlayAdapterPort(
        NetworkProvider providerConfig,
        OvsBridgePortInfo portInfo) =>
        portInfo.Interfaces.Count switch
        {
            > 1 => UpdateBondedOverlayAdapterPort(providerConfig, portInfo),
            // Simple adapter ports (no bond) do not have any settings which can be changed
            _ => unitAff
        };

    private Aff<RT, Unit> UpdateBondedOverlayAdapterPort(
        NetworkProvider providerConfig,
        OvsBridgePortInfo portInfo) =>
        from _1 in unitAff
        let expectedBondMode = GetBondMode(providerConfig.BridgeOptions?.BondMode)
        from _2 in portInfo.BondMode == expectedBondMode
            ? unitAff
            : AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBondPort(portInfo.PortName, expectedBondMode, ct).ToAff(e => e)
                    select unit),
                _ => true,
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    // We force a bond mode during the rollback as no bond mode at all
                    // can break the physical network. When a bond port is configured,
                    // the bond mode should be set anyway.
                    from _ in ovs.UpdateBondPort(portInfo.PortName, portInfo.BondMode.IfNone("active-backup"), ct).ToAff(e => e)
                    select unit),
                NetworkChangeOperation.UpdateBondPort,
                portInfo.PortName, providerConfig.BridgeName!)
        select unit;

    public Aff<RT, Unit> UpdateBridgeMappings(NetworkProvidersConfiguration newConfig) =>
        from _1 in LogTrace("Entering {Method}", nameof(UpdateBridgeMappings))
        from ovsTable in timeout(
            TimeSpan.FromSeconds(30),
            from ovs in default(RT).OVS.ToAff()
            from ct in cancelToken<RT>()
            from t in ovs.GetOVSTable(ct).ToAff(l => l)
            select t)
        let currentMappings = ovsTable.ExternalIds.Find("ovn-bridge-mappings")
        let expectedMappings = string.Join(',', newConfig.NetworkProviders.ToSeq()
            .Filter(x => notEmpty(x.BridgeName))
            .Map(networkProvider => $"{networkProvider.Name}:{networkProvider.BridgeName}"))
        from _2 in currentMappings != expectedMappings
            ? AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS.ToAff()
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgeMapping(expectedMappings, ct).ToAff(e => e)
                    select unit),
                NetworkChangeOperation.UpdateBridgeMapping)
            : unitAff
        select unit;

    private static string GetNetNatName(string providerName)
        // The pattern for the NetNat name should be "eryph_{providerName}_{subnetName}".
        // At the moment, we only support a single provider subnet which must be named
        // 'default'. Hence, we hardcode the subnet part for now.
        => $"eryph_{providerName}_default";

    private static string GetBondPortName(string bridgeName)
        => $"{bridgeName}-bond";

    private static string GetBondMode(BondMode? bondMode) =>
        bondMode switch
        {
            BondMode.BalanceSlb => "balance-slb",
            BondMode.BalanceTcp => "balance-tcp",
            _ => "active-backup",
        };

    private static Aff<RT, Unit> StopOVN() =>
        timeout(
            TimeSpan.FromSeconds(10),
            from _1 in Logger<RT>.logDebug<NetworkChangeOperation<RT>>("Stopping OVN controller")
            from syncClient in default(RT).AgentSync
            from ct in cancelToken<RT>()
            from _2 in syncClient.SendSyncCommand("STOP_OVN", ct)
            select unit);

    private static Aff<RT, Unit> StartOVN() =>
        timeout(
            TimeSpan.FromSeconds(10),
            from _1 in Logger<RT>.logDebug<NetworkChangeOperation<RT>>("Starting OVN controller")
            from syncClient in default(RT).AgentSync
            from ct in cancelToken<RT>()
            from _2 in syncClient.SendSyncCommand("START_OVN", ct)
            select unit);

    private Aff<RT, Unit> AddOperation(
        Func<Aff<RT, Unit>> func,
        NetworkChangeOperation operation,
        params object[] args) =>
        from _1 in LogTrace("Adding operation {Operation}. Args: {Args} ",
            operation, args)
        from _2 in Eff(() =>
        {
            _operations = _operations.Add(new NetworkChangeOperation<RT>(
                operation, func, null, null, args));
            return unit;
        })
        select unit;

    private Aff<RT, Unit> AddOperation(
        Func<Aff<RT, Unit>> func,
        Func<Seq<NetworkChangeOperation>, bool> canRollback,
        Func<Aff<RT, Unit>> rollBackFunc,
        NetworkChangeOperation operation,
        params object[] args) =>
        from _1 in LogTrace("Adding rollback enabled operation {Operation}. Args: {Args} ",
            operation, args)
        from _2 in Eff(() =>
        {
            _operations = _operations.Add(new NetworkChangeOperation<RT>(
                operation, func, canRollback, rollBackFunc, args));
            return unit;
        })
        select unit;

    private Aff<RT, Unit> LogDebug(string message, params object?[] args) =>
        Logger<RT>.logDebug<NetworkChangeOperationBuilder<RT>>(message, args);

    private Eff<RT, Unit> LogTrace(string message, params object?[] args) =>
        Logger<RT>.logTrace<NetworkChangeOperationBuilder<RT>>(message, args);
}
