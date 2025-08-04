using System;
using System.Linq;
using System.Net;
using System.Threading;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Networks;

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
        from _1 in LogMethodTrace(nameof(CreateOverlaySwitch))
        from _2 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.CreateOverlaySwitch(adapters)
                  select unit,
            false,
            NetworkChangeOperation.CreateOverlaySwitch)
        from _3 in AddOperation(
            StartOVN,
            false,
            NetworkChangeOperation.StartOVN)
        select unit;

    public Aff<RT, OvsBridgesInfo> RebuildOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo ovsBridges,
        Seq<string> newOverlayAdapters) =>
        from _1 in LogMethodTrace(nameof(RebuildOverlaySwitch))
        // After the overlay switch has been rebuilt and the integration
        // bridge br-int has been recreated, we must recreate the ports
        // for the currently running catlets.
        let vmPortNames = ovsBridges.Bridges.Find("br-int").ToSeq()
            .Bind(b => b.Ports.Values.ToSeq()
                // We assume that the port is a catlet port when the port name
                // matches the interface ID (iface-id) as this the pattern which
                // we use and the iface-id should only be set by the hypervisor (i.e us).
                .Filter(p => p.Interfaces.Exists(i => i.InterfaceId.Map(iid => iid == p.Name).IfNone(false))))
            .Map(p => p.Name)
            .Strict()
        from _2 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: a => from _1 in LogDebug("Found adapters on overlay switch. Adding disconnect and reconnect operations.")
                      from _2 in DisconnectOverlayVmAdapters(a)
                      select unit)
        from _3 in AddOperation(
            StopOVN,
            _ => true,
            StartOVN,
            false,
            NetworkChangeOperation.StopOVN)
        from _4 in ovsBridges.Bridges.Keys
            .Map(AddRemoveBridgeOperation)
            .SequenceSerial()
        from _5 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.RemoveOverlaySwitch()
                  from _2 in hostCommands.CreateOverlaySwitch(newOverlayAdapters)
                  select unit,
            false,
            NetworkChangeOperation.RebuildOverLaySwitch)
        from _6 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: ConnectOverlayVmAdapters)
        // When OVN is started, it automatically creates the integration bridge br-int.
        from _7 in AddOperation(
            StartOVN,
            false,
            NetworkChangeOperation.StartOVN)
        from _8 in vmPortNames.Match(
            Empty: () => unitAff,
            Seq: RecreateVmPorts)
        select new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>());

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
            false,
            NetworkChangeOperation.DisconnectVMAdapters);

    private Aff<RT, Unit> ConnectOverlayVmAdapters(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters) =>
        AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.ConnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName)
                  select unit,
            false,
            NetworkChangeOperation.ConnectVMAdapters);

    private Aff<RT, Unit> RecreateVmPorts(
        Seq<string> portNames) =>
        AddOperation(
            () => from _1 in portNames
                    .Map(portName => timeout(
                        TimeSpan.FromSeconds(30),
                        from ovs in default(RT).OVS.ToAff()
                        from ct in cancelToken<RT>()
                        from _1 in ovs.AddPortWithIFaceId("br-int", portName, ct).ToAff(e => e)
                        select unit))
                    .SequenceParallel()
                  select unit,
            false,
            NetworkChangeOperation.RecreateVmPorts,
            "br-int");

    private Aff<RT, Unit> AddRemoveBridgeOperation(string bridgeName) =>
        from _1 in LogDebug("Adding operation to remove bridge {bridge}", bridgeName)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS.ToAff()
                from ct in cancelToken<RT>()
                from _ in ovs.RemoveBridge(bridgeName, ct).ToAff(e => e)
                select unit),
            false,
            NetworkChangeOperation.RemoveBridge, 
            bridgeName)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo currentBridges) =>
        from _1 in LogMethodTrace(nameof(RemoveOverlaySwitch))
        from _2 in overlayVMAdapters.Match(
            Empty: () => unitAff,
            Seq: a => from _1 in LogDebug("Found adapters on overlay switch. Adding disconnect operations.")
                      from _2 in DisconnectOverlayVmAdapters(a)
                      select unit)
        from _3 in AddOperation(
            StopOVN,
            _ => true,
            StartOVN,
            false,
            NetworkChangeOperation.StopOVN)
        from _4 in currentBridges.Bridges.Keys
            .Map(AddRemoveBridgeOperation)
            .SequenceSerial()
        from _5 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _ in hostCommands.RemoveOverlaySwitch()
                  select unit,
            false,
            NetworkChangeOperation.RemoveOverlaySwitch)
        select new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>());
    
    public Aff<RT, Unit> EnableSwitchExtension(
        Guid switchId,
        string switchName) =>
        from _1 in LogMethodTrace(nameof(EnableSwitchExtension))
        from _2 in AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                      from _ in hostCommands.EnableSwitchExtension(switchId)
                      select unit,
                false,
                NetworkChangeOperation.EnableSwitchExtension,
                switchName)
        select unit;

    public Aff<RT, Unit> DisableSwitchExtension(
        Guid switchId,
        string switchName) =>
        from _1 in LogMethodTrace(nameof(DisableSwitchExtension))
        from _2 in AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                      from _ in hostCommands.DisableSwitchExtension(switchId)
                      select unit,
                false,
                NetworkChangeOperation.DisableSwitchExtension,
                switchName)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveUnusedBridges(
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> expectedBridges) =>
        from _1 in LogMethodTrace(nameof(RemoveUnusedBridges))
        let newBridgeNames = toHashSet(expectedBridges.Map(b => b.BridgeName))
        let unusedBridges = ovsBridges.Bridges.Values.ToSeq()
            .Filter(b => b.Name != "br-int")
            .Filter(b => !newBridgeNames.Contains(b.Name))
            .Map(b => b.Name)
        from _2 in unusedBridges
            .Map(RemoveUnusedBridge)
            .SequenceSerial()
        select ovsBridges.RemoveBridges(unusedBridges);

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
            false,
            NetworkChangeOperation.RemoveUnusedBridge,
            bridgeName)
        select unit;

    private static (Option<int> VlanTag, Option<string> VlanMode) GetBridgePortSettings(
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

    public Aff<RT, HashSet<string>> AddMissingBridges(
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> expectedBridges) =>
        from _1 in LogMethodTrace(nameof(AddMissingBridges))
        let missingBridges = expectedBridges
            .Filter(b => !ovsBridges.Bridges.Find(b.BridgeName).IsSome)
        from _2 in missingBridges
            .Map(AddMissingBridge)
            .SequenceSerial()
        let missingBridgeNames = missingBridges.Map(b => b.BridgeName)
        select toHashSet(missingBridgeNames);

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
            false,
            NetworkChangeOperation.AddBridge,
            newBridge.BridgeName)
        select unit;

    public Aff<RT, Seq<NetNat>> RemoveInvalidNats(
        Seq<NetNat> netNats,
        Seq<NewBridge> expectedBridges) =>
        from _1 in LogMethodTrace(nameof(RemoveInvalidNats))
        from removedNats in netNats.Filter(n => n.Name.StartsWith("eryph_"))
            .Map(n => RemoveInvalidNat(n, expectedBridges))
            .SequenceSerial()
        select netNats.Except(removedNats.Somes()).ToSeq();

    private Aff<RT, Option<NetNat>> RemoveInvalidNat(
        NetNat nat,
        Seq<NewBridge> expectedBridges) =>
        from _ in unitAff
        // When the prefix of the NetNat is invalid, we will just recreate the NetNat.
        let natPrefix = parseIPNetwork2(nat.InternalIPInterfaceAddressPrefix)
        let expectedNat = expectedBridges
            .Bind(b => b.Nat.ToSeq())
            .Find(n => n.NatName == nat.Name)
        let isNatValid = expectedNat.Map(b => b.Network == natPrefix).IfNone(false)
        from result in isNatValid
            ? SuccessAff(Option<NetNat>.None)
            : from _1 in LogDebug("Removing invalid host NAT '{Nat}'", nat.Name)
              from _2 in AddOperation(
                  () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                        from _ in hostCommands.RemoveNetNat(nat.Name)
                        select unit,
                  false,
                  NetworkChangeOperation.RemoveNetNat,
                  nat.Name)
              select Some(nat)
        select result;

    public Aff<RT, Unit> AddMissingNats(
        Seq<NewBridge> expectedBridges,
        Seq<NetNat> netNats) =>
        from _1 in unitAff
        let unmanagedNats = netNats
            .Filter(n => !n.Name.StartsWith("eryph_"))
            .Map(n => from network in parseIPNetwork2(n.InternalIPInterfaceAddressPrefix)
                      select (n.Name, Network: network))
            .Somes()
        from _2 in expectedBridges
            // Any invalid NATs would have been removed earlier. When the NAT
            // still exists at this point, it is valid.
            .Filter(b => b.Nat.Match(
                Some: newNat => !netNats.Exists(n => n.Name == newNat.NatName),
                None: () => false))
            .Map(b => AddMissingNat(b, unmanagedNats))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> AddMissingNat(
        NewBridge expectedBridge,
        Seq<(string Name, IPNetwork2 Network)> unmanagedNats) =>
        from newNat in expectedBridge.Nat
            .ToAff(Error.New($"BUG! NAT bridge '{expectedBridge.BridgeName}' has no NAT configuration."))
        from _1 in unmanagedNats.Find(un => un.Network.Overlap(newNat.Network)).Match(
            Some: un => FailEff<Unit>(Error.New(
                $"The IP range '{newNat.Network}' of the provider '{expectedBridge.ProviderName}' overlaps "
                    + $"the IP range '{un.Network}' of the NAT '{un.Name}' which is not managed by eryph.")),
            None: () => unitEff)
        from _2 in AddOperation(
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.AddNetNat(newNat.NatName, newNat.Network)
                  select unit,
            _ => true,
            () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                  from _1 in hostCommands.RemoveNetNat(newNat.NatName)
                  select unit,
            false,
            NetworkChangeOperation.AddNetNat,
            newNat.NatName, newNat.Network)
        select unit;

    public Aff<RT, OvsBridgesInfo> RemoveInvalidAdapterPortsFromBridges(
        Seq<NewBridge> expectedBridges,
        HostAdaptersInfo adaptersInfo,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogMethodTrace(nameof(RemoveInvalidAdapterPortsFromBridges))
        from changedBridges in expectedBridges
            .Map(newBridge => from ovsBridge in ovsBridges.Bridges.Find(newBridge.BridgeName)
                              select (NewBridge: newBridge, OvsBridge: ovsBridge))
            .Somes()
            .Map(r => RemoveInvalidAdapterPortsFromBridge(r.NewBridge, r.OvsBridge, adaptersInfo))
            .SequenceSerial()
        let result = changedBridges.Fold(ovsBridges, (s, b) => s.SetBridge(b))
        select result;

    public Aff<RT, OvsBridgeInfo> RemoveInvalidAdapterPortsFromBridge(
        NewBridge expectedBridge,
        OvsBridgeInfo ovsBridge,
        HostAdaptersInfo adaptersInfo) =>
        from _1 in unitAff
        let portsWithPhysicalInterfaces = ovsBridge.Ports.Values.ToSeq()
            .Filter(p => p.Interfaces.Exists(i => i.IsExternal))
        // There must always be at most one port with external interfaces.
        // When multiple external interfaces should be used, the port should be bonded.
        from portCheckResult in portsWithPhysicalInterfaces.Match(
            Empty: () => SuccessEff((true, false)),
            Head: port => IsAdapterPortValid(expectedBridge, port, adaptersInfo),
            Tail: _ => SuccessEff((false, false)))
        from result in portCheckResult.IsValid switch
        {
            true => SuccessAff(ovsBridge),
            false => from _ in portsWithPhysicalInterfaces
                        .Map(pi => RemoveInvalidAdapterPortFromBridge(pi, portCheckResult.AnyAdapterRenamed))
                        .SequenceSerial()
                     select ovsBridge.RemovePorts(portsWithPhysicalInterfaces.Map(p => p.Name)),
        }
        select result;

    private static Eff<RT, (bool IsValid, bool AnyAdapterRenamed)> IsAdapterPortValid(
        NewBridge expectedBridge,
        OvsBridgePortInfo port,
        HostAdaptersInfo adaptersInfo) =>
        from expectedAdapterInfos in expectedBridge.Adapters
            .Map(a => adaptersInfo.Adapters.Find(a)
                .ToEff(Error.New($"The adapter '{a}' of the provider '{expectedBridge.ProviderName}' does not exist.")))
            .Sequence()
        let anyAdapterRenamed = expectedAdapterInfos
            .Exists(a => a.ConfiguredName.Map(cn => cn != a.Name).IfNone(false))
        let expectedAdapters = toHashSet(expectedAdapterInfos.Map(a => (a.Name, HostInterfaceId: Some(a.InterfaceId))))
        let actualAdapters = toHashSet(port.Interfaces.Map(i => (i.Name, i.HostInterfaceId)))
        let isValid = expectedAdapters == actualAdapters
        select (isValid, anyAdapterRenamed);

    private Aff<RT, Unit> RemoveInvalidAdapterPortFromBridge(
        OvsBridgePortInfo port,
        bool force) =>
        from _1 in LogDebug("The port {PortName} of bridge {BridgeName} uses unnecessary or invalid external interfaces. Removing the port.",
            port.Name, port.BridgeName)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS.ToAff()
                from ct in cancelToken<RT>().ToAff()
                from _ in ovs.RemovePort(port.BridgeName, port.Name, ct).ToAff(l => l)
                select unit),
            force,
            NetworkChangeOperation.RemoveAdapterPort,
            port.Name, port.BridgeName)
        select unit;

    public Aff<RT, Unit> ConfigureNatAdapters(
        Seq<NewBridge> expectedBridges,
        HashSet<string> createdBridges) =>
        from _1 in LogMethodTrace(nameof(ConfigureNatAdapters))
        from _2 in expectedBridges
            .Filter(newBridge => newBridge.ProviderType is NetworkProviderType.NatOverlay)
            .Map(newBridge => ConfigureNatAdapter(newBridge, createdBridges))
            .SequenceSerial()
        select unit;
    
    private Aff<RT, Unit> ConfigureNatAdapter(
        NewBridge expectedBridge,
        HashSet<string> createdBridges) =>
        from newNat in expectedBridge.Nat
            .ToAff(Error.New($"BUG! NAT bridge '{expectedBridge.BridgeName}' of provider '{expectedBridge.ProviderName}' has no NAT configuration."))
        // When the bridge is new, we do not need to check the IP address.
        // This check also prevents us from skipping the assignment of the
        // IP address when the bridge is recreated.
        from isBridgeAdapterIpValid in createdBridges.Contains(expectedBridge.BridgeName)
            ? SuccessAff(false)
            : IsNatAdapterIpValid(expectedBridge.BridgeName, newNat)
        from _1 in isBridgeAdapterIpValid
            ? unitAff   
            : AddOperation(
                () => from hostCommands in default(RT).HostNetworkCommands.ToAff()
                    from _1 in hostCommands.EnableBridgeAdapter(expectedBridge.BridgeName)
                    from _2 in hostCommands.ConfigureAdapterIp(
                        expectedBridge.BridgeName, newNat.Gateway, newNat.Network)
                    select unit,
                false,
                NetworkChangeOperation.ConfigureNatIp,
                expectedBridge.BridgeName)
        select unit;

    private Aff<RT, bool> IsNatAdapterIpValid(
        string adapterName,
        NewBridgeNat expectedNat) =>
        from hostCommands in default(RT).HostNetworkCommands.ToAff()
        from ipAddresses in hostCommands.GetAdapterIpV4Address(adapterName)
        from isValid in ipAddresses.Match(
            Empty: () => SuccessAff(false),
            Head: ip => from _1 in unitAff
                        let isMatch = ip.PrefixLength == expectedNat.Network.Cidr
                                      && ip.IPAddress == expectedNat.Gateway.ToString()
                        from _2 in isMatch
                            ? unitAff
                            : LogDebug("The host NAT adapter '{AdapterName}' has an invalid IP address. Expected: {ExpectedIp}/{ExpectedSuffix}, Actual: {ActualIp}/{ActualSuffix}",
                                adapterName, expectedNat.Gateway, expectedNat.Network.Cidr, ip.IPAddress, ip.PrefixLength)
                        select isMatch,
            Tail: _ => from _1 in LogDebug("The host NAT adapter '{AdapterName}' has multiple IP addresses.",
                           adapterName)
                       select false)
        select isValid;
    
    /// <summary>
    /// This method removes OVS bridges for which the host network adapter no longer exist.
    /// The bridges will be recreated later.
    /// </summary>
    public Aff<RT, OvsBridgesInfo> RemoveBridgesWithMissingBridgeAdapter(
        Seq<NewBridge> expectedBridges,
        HostAdaptersInfo hostAdaptersInfo,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogMethodTrace(nameof(RemoveBridgesWithMissingBridgeAdapter))
        let bridgesToRemove = expectedBridges
            .Filter(expectedBridge => ovsBridges.Bridges.Find(expectedBridge.BridgeName).IsSome)
            .Filter(expectedBridge => hostAdaptersInfo.Adapters
                .Find(expectedBridge.BridgeName)
                .Filter(a => !a.IsPhysical)
                .IsNone)
            .Map(expectedBridge => expectedBridge.BridgeName)
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
                    false,
                    NetworkChangeOperation.RemoveMissingBridge,
                    bridgeName)
                select unit)
            .SequenceSerial()
        select ovsBridges.RemoveBridges(bridgesToRemove);

    public Aff<RT, Unit> UpdateBridgePorts(
        Seq<NewBridge> expectedBridges,
        HashSet<string> createdBridges,
        OvsBridgesInfo ovsBridges) =>
        from _1 in LogMethodTrace(nameof(UpdateBridgePorts))
        from _2 in expectedBridges
            .Filter(expectedBridge => !createdBridges.Contains(expectedBridge.BridgeName))
            .Map(expectedBridge => UpdateBridgePort(expectedBridge, ovsBridges))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> UpdateBridgePort(
        NewBridge expectedBridge,
        OvsBridgesInfo ovsBridgesInfo) =>
        from ovsBridgeInfo in ovsBridgesInfo.Bridges.Find(expectedBridge.BridgeName)
            .ToAff(Error.New($"BUG! Bridge '{expectedBridge.BridgeName}' is missing during bridge port update."))
        from ovsBridgePort in ovsBridgeInfo.Ports.Find(expectedBridge.BridgeName)
            .ToAff(Error.New($"BUG! Bridge port '{expectedBridge.BridgeName}' is missing during bridge port update."))
        let expectedPortSettings = GetBridgePortSettings(expectedBridge.Options)
        let currentPortSettings = (ovsBridgePort.Tag, ovsBridgePort.VlanMode)
        from _ in expectedPortSettings == currentPortSettings
            ? unitAff
            : AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgePort(expectedBridge.BridgeName, expectedPortSettings.VlanTag, expectedPortSettings.VlanMode, ct)
                            .ToAff(e => e)
                    select unit),
                _ => true,
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgePort(expectedBridge.BridgeName, currentPortSettings.Tag , currentPortSettings.VlanMode, ct)
                        .ToAff(e => e)
                    select unit), 
                false,
                NetworkChangeOperation.UpdateBridgePort,
                expectedBridge.BridgeName)
        select unit;

    public Aff<RT, Unit> ConfigureOverlayAdapterPorts(
        Seq<NewBridge> expectedBridges,
        OvsBridgesInfo ovsBridges,
        HostAdaptersInfo hostAdaptersInfo) =>
        from _1 in LogMethodTrace(nameof(ConfigureOverlayAdapterPorts))
        from _2 in expectedBridges
            .Filter(expectedBridge => expectedBridge.ProviderType is NetworkProviderType.Overlay)
            .Filter(expectedBridge => expectedBridge.Adapters.Count > 0)
            .Map(expectedBridge => ConfigureOverlayAdapterPort(expectedBridge, ovsBridges, hostAdaptersInfo))
            .SequenceSerial()
        select unit;

    private Aff<RT, Unit> ConfigureOverlayAdapterPort(
        NewBridge expectedBridge,
        OvsBridgesInfo ovsBridges,
        HostAdaptersInfo hostAdaptersInfo) =>
        from expectedPortName in expectedBridge.Adapters.Match(
            Empty: () => FailEff<string>(Error.New("BUG! No adapters configured when trying to add overlay adapters.")),
            Head: a => from adapterInfo in FindHostAdapter(hostAdaptersInfo, a, expectedBridge.BridgeName)
                       // When the adapter has been renamed, the config and hence the expected
                       // bridge will contain the old name. We must look up the current adapter name.
                       select adapterInfo.Name,
            Tail: _ => SuccessEff(GetBondPortName(expectedBridge.BridgeName)))
        // When the port uses the wrong adapters, we already removed the port earlier.
        // Hence, if the port exists, the adapters are correct, and we only need to check the bond setting.
        let existingPort = ovsBridges.Bridges
            .Find(expectedBridge.BridgeName)
            .Bind(b => b.Ports.Find(expectedPortName))
        from _4 in  existingPort.Match(
            None: () => AddOverlayAdapterPort(expectedBridge, expectedPortName, hostAdaptersInfo),
            Some: portInfo => UpdateOverlayAdapterPort(expectedBridge, portInfo))
        select unit;
    
    private Aff<RT, Unit> AddOverlayAdapterPort(
        NewBridge expectedBridge,
        string expectedPortName,
        HostAdaptersInfo hostAdaptersInfo) =>
        from expectedAdapters in expectedBridge.Adapters
            .Map(adapterName => FindHostAdapter(hostAdaptersInfo, adapterName, expectedBridge.BridgeName))
            .Sequence()
        let anyAdapterRenamed = expectedAdapters
            .Exists(a => a.ConfiguredName.Map(cn => cn != a.Name).IfNone(false))
        from _2 in expectedAdapters.Match(
                Empty: () => unitAff,
                Head: a => AddSimpleOverlayAdapterPort(expectedBridge, expectedPortName, a, anyAdapterRenamed),
                Tail: (h, t) => AddBondedOverlayAdapterPort(expectedBridge, expectedPortName, h.Cons(t), anyAdapterRenamed))
        select unit;

    private static Eff<HostAdapterInfo> FindHostAdapter(
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
        NewBridge expectedBridge,
        string expectedPortName,
        Seq<HostAdapterInfo> adapters,
        bool force) =>
        from _1 in unitAff
        let interfaces = adapters
            .Map(a => new OvsInterfaceUpdate(a.Name, a.InterfaceId, a.ConfiguredName.IfNone(a.Name)))
        let ovsBondMode = GetBondMode(expectedBridge.Options)
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.AddBond(expectedBridge.BridgeName, expectedPortName, interfaces, ovsBondMode, ct).ToAff(e => e)
                select unit),
            _ => true,
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(expectedBridge.BridgeName, expectedPortName, ct).ToAff(e => e)
                select unit),
            force,
            NetworkChangeOperation.AddBondPort,
            expectedPortName, string.Join(", ", adapters.Map(a => a.Name)), expectedBridge.BridgeName)
        select unit;

    private Aff<RT, Unit> AddSimpleOverlayAdapterPort(
        NewBridge expectedBridge,
        string expectedPortName,
        HostAdapterInfo adapter,
        bool force) =>
        from _1 in guard(expectedPortName == adapter.Name,
                Error.New($"BUG! Mismatch of port name {expectedPortName} and adapter name {adapter.Name}."))
            .ToAff()
        let interfaceUpdate = new OvsInterfaceUpdate(
            adapter.Name,
            adapter.InterfaceId,
            adapter.ConfiguredName.IfNone(adapter.Name))
        from _2 in AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.AddPort(expectedBridge.BridgeName, interfaceUpdate, ct).ToAff(e => e)
                select unit),
            _ => true,
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(expectedBridge.BridgeName, adapter.Name, ct).ToAff(e => e)
                select unit),
            force,
            NetworkChangeOperation.AddAdapterPort,
            adapter.Name, expectedBridge.BridgeName)
        select unit;

    private Aff<RT, Unit> UpdateOverlayAdapterPort(
        NewBridge expectedBridge,
        OvsBridgePortInfo portInfo) =>
        portInfo.Interfaces.Count switch
        {
            > 1 => UpdateBondedOverlayAdapterPort(expectedBridge, portInfo),
            // Simple adapter ports (no bond) do not have any settings which can be changed
            _ => unitAff
        };

    private Aff<RT, Unit> UpdateBondedOverlayAdapterPort(
        NewBridge expectedBridge,
        OvsBridgePortInfo portInfo) =>
        from _1 in unitAff
        let expectedBondMode = GetBondMode(expectedBridge.Options)
        from _2 in portInfo.BondMode == expectedBondMode
            ? unitAff
            : AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBondPort(portInfo.Name, expectedBondMode, ct).ToAff(e => e)
                    select unit),
                _ => true,
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    // We force a bond mode during the rollback as no bond mode at all
                    // can break the physical network. When a bond port is configured,
                    // the bond mode should be set anyway.
                    from _ in ovs.UpdateBondPort(portInfo.Name, portInfo.BondMode.IfNone("active-backup"), ct).ToAff(e => e)
                    select unit),
                false,
                NetworkChangeOperation.UpdateBondPort,
                portInfo.Name, expectedBridge.BridgeName)
        select unit;

    public Aff<RT, Unit> UpdateBridgeMappings(
        Seq<NewBridge> expectedBridges) =>
        from _1 in LogMethodTrace(nameof(UpdateBridgeMappings))
        from ovsTable in timeout(
            TimeSpan.FromSeconds(30),
            from ovs in default(RT).OVS.ToAff()
            from ct in cancelToken<RT>()
            from t in ovs.GetOVSTable(ct).ToAff(l => l)
            select t)
        let currentMappings = ovsTable.ExternalIds.Find("ovn-bridge-mappings")
        let expectedMappings = string.Join(',', expectedBridges
            .Map(expectedBridge => $"{expectedBridge.ProviderName}:{expectedBridge.BridgeName}"))
        from _2 in currentMappings != expectedMappings
            ? AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS.ToAff()
                    from ct in cancelToken<RT>()
                    from _ in ovs.UpdateBridgeMapping(expectedMappings, ct).ToAff(e => e)
                    select unit),
                false,
                NetworkChangeOperation.UpdateBridgeMapping)
            : unitAff
        select unit;

    private static string GetBondPortName(string bridgeName)
        => $"{bridgeName}-bond";

    private static string GetBondMode(
        Option<NetworkProviderBridgeOptions> bridgeOptions) =>
        bridgeOptions.Bind(o => Optional(o.BondMode))
            // balance-tcp is not supported as it seems to require LACP
            // which does not seem to work in Hyper-V.
            .Bind(bondMode => bondMode is BondMode.BalanceSlb ? Some("balance-slb") : None)
            .IfNone("active-backup");

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
        bool force,
        NetworkChangeOperation operation,
        params object[] args) =>
        from _1 in LogTrace("Adding operation {Operation}. Force: {Force}. Args: {Args} ",
            operation, force, args)
        from _2 in Eff(() =>
        {
            _operations = _operations.Add(new NetworkChangeOperation<RT>(
                operation, func, null, null, force, args));
            return unit;
        })
        select unit;

    private Aff<RT, Unit> AddOperation(
        Func<Aff<RT, Unit>> func,
        Func<Seq<NetworkChangeOperation>, bool> canRollback,
        Func<Aff<RT, Unit>> rollBackFunc,
        bool force,
        NetworkChangeOperation operation,
        params object[] args) =>
        from _1 in LogTrace("Adding rollback enabled operation {Operation}. Force: {Force}. Args: {Args} ",
            operation, force, args)
        from _2 in Eff(() =>
        {
            _operations = _operations.Add(new NetworkChangeOperation<RT>(
                operation, func, canRollback, rollBackFunc, force, args));
            return unit;
        })
        select unit;

    private static Aff<RT, Unit> LogDebug(string message, params object?[] args) =>
        Logger<RT>.logDebug<NetworkChangeOperationBuilder<RT>>(message, args);

    private static Eff<RT, Unit> LogTrace(string message, params object?[] args) =>
        Logger<RT>.logTrace<NetworkChangeOperationBuilder<RT>>(message, args);

    private static Eff<RT, Unit> LogMethodTrace(string methodName) =>
        Logger<RT>.logTrace<NetworkChangeOperationBuilder<RT>>(
            "Entering {Method}", methodName);
}
