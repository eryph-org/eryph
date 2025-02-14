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
using LanguageExt.UnsafeValueAccess;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;
using Array = System.Array;

namespace Eryph.Modules.VmHostAgent.Networks;

public enum NetworkChangeOperation
{

    StartOVN,
    StopOVN,

    CreateOverlaySwitch,
    RebuildOverLaySwitch,
    RemoveOverlaySwitch,

    DisconnectVMAdapters,
    ConnectVMAdapters,

    RemoveBridge,
    RemoveUnusedBridge,
    RemoveMissingBridge,
    AddBridge,

    AddNetNat,
    RemoveNetNat,

    RemoveAdapterPort,
    AddAdapterPort,
    AddBondPort,
    UpdateBridgePort,

    ConfigureNatIp,
    UpdateBridgeMapping,

    EnableSwitchExtension,
    DisableSwitchExtension
}

public class NetworkChangeOperationBuilder<RT> where RT : struct,
    HasCancel<RT>,
    HasOVSControl<RT>,
    HasHostNetworkCommands<RT>,
    HasAgentSyncClient<RT>,
    HasLogger<RT>
{
    private Seq<NetworkChangeOperation<RT>> _operations;
    private ILogger _logger;

    private static Aff<RT, Unit> StopOVN()
    {
        var cts = new CancellationTokenSource(10000);

        return
            from l1 in Logger<RT>.logDebug<NetworkChangeOperationBuilder<RT>>("Stopping ovn controller")
            from syncClient in default(RT).AgentSync.Bind(c => c
                .SendSyncCommand("STOP_OVN", cts.Token))
            select unit;

    }

    private static Aff<RT, Unit> StartOVN()
    {
        var cts = new CancellationTokenSource(10000);

        return
            from l1 in Logger<RT>.logDebug<NetworkChangeOperationBuilder<RT>>("Starting ovn controller")
            from syncClient in default(RT).AgentSync.Bind(c => c
                .SendSyncCommand("START_OVN", cts.Token))
            select unit;

    }

    internal static Aff<RT, NetworkChangeOperationBuilder<RT>> New()
    {
        return default(RT).Logger<NetworkChangeOperationBuilder<RT>>().Map(
            logger => new NetworkChangeOperationBuilder<RT>(logger));
    }

    internal NetworkChangeOperationBuilder(ILogger logger)
    {
        _logger = logger;
    }

    private Unit AddOperation(Func<Aff<RT, Unit>> func, NetworkChangeOperation operation, params object[] args)
    {
        _logger.LogTrace("Adding operation {operation}. Args: {args} ", operation, args);
        _operations = _operations.Add(new NetworkChangeOperation<RT>(operation, func, null, null, args));
        return Unit.Default;
    }

    private Unit AddOperation(Func<Aff<RT, Unit>> func, Func<Seq<NetworkChangeOperation>, bool> canRollback, Func<Aff<RT, Unit>> rollBackFunc, NetworkChangeOperation operation, params object[] args)
    {
        _logger.LogTrace("Adding rollback enabled operation {operation}. Args: {args} ", operation, args);

        _operations = _operations.Add(new NetworkChangeOperation<RT>(operation, func, canRollback, rollBackFunc, args));
        return Unit.Default;
    }

    private Aff<RT, Unit> AddOperationRt(
        Func<Aff<RT, Unit>> func,
        NetworkChangeOperation operation,
        params object[] args)
    {
        _logger.LogTrace("Adding operation {operation}. Args: {args} ", operation, args);
        _operations = _operations.Add(new NetworkChangeOperation<RT>(operation, func, null, null, args));
        return unitAff;
    }

    private Aff<RT, Unit> AddOperationRt(
        Func<Aff<RT, Unit>> func,
        Func<Seq<NetworkChangeOperation>, bool> canRollback,
        Func<Aff<RT, Unit>> rollBackFunc,
        NetworkChangeOperation operation,
        params object[] args)
    {
        _logger.LogTrace("Adding rollback enabled operation {operation}. Args: {args} ", operation, args);

        _operations = _operations.Add(new NetworkChangeOperation<RT>(operation, func, canRollback, rollBackFunc, args));
        return unitAff;
    }

    private Aff<RT, IDisposable> BeginScope(string methodName) => 
        Eff<RT, IDisposable>(_ => _logger.BeginScope("Method: {Method}", methodName));

    private Aff<RT, Unit> LogDebug(string? message, params object?[] args) =>
        Eff<RT, Unit>(_ => { _logger.LogDebug(message, args); return unit; });
    

    public Aff<RT, Unit> CreateOverlaySwitch(Seq<string> adapters)
    {
        using (_logger.BeginScope("Method: {method}", nameof(CreateOverlaySwitch)))
        {
            AddOperation(
                () => default(RT).HostNetworkCommands.Bind(c => c
                    .CreateOverlaySwitch(adapters)),
                NetworkChangeOperation.CreateOverlaySwitch);

            AddOperation(
                StartOVN,
                NetworkChangeOperation.StartOVN);

            return unitAff;
        }
    }

    public Aff<RT, OvsBridgesInfo> RebuildOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo ovsBridges,
        HashSet<string> newOverlayAdapters)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RebuildOverlaySwitch)))
        {

            if (overlayVMAdapters.Count > 0)
            {
                _logger.LogDebug("Found adapters on overlay switch. Adding disconnect and reconnect operations.");
                AddOperation(
                    () => default(RT).HostNetworkCommands.Bind(c => c
                        .DisconnectNetworkAdapters(overlayVMAdapters)),
                    _ => true,
                    () => default(RT).HostNetworkCommands.Bind(c => c
                        .ReconnectNetworkAdapters(overlayVMAdapters))
                    ,
                    NetworkChangeOperation.DisconnectVMAdapters);
            }

            AddOperation(StopOVN, _ => true, StartOVN,
                NetworkChangeOperation.StopOVN);

            foreach (var bridgeName in ovsBridges.Bridges.Keys)
            {
                _logger.LogDebug("Adding operation to remove bridge {bridge}", bridgeName);

                AddOperation(
                    () => default(RT).OVS.Bind(o =>
                    {
                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        return o.RemoveBridge(bridgeName, cancelSourceCommand.Token).ToAff(e => e);
                    }),
                    NetworkChangeOperation.RemoveBridge, bridgeName);
            }

            AddOperation(() =>
                    default(RT).HostNetworkCommands.Bind(c => c
                        .RemoveOverlaySwitch()
                        .Bind(_ => c.CreateOverlaySwitch(newOverlayAdapters))),
                NetworkChangeOperation.RebuildOverLaySwitch
            );

            if (overlayVMAdapters.Length > 0)
            {
                AddOperation(
                    () => default(RT).HostNetworkCommands.Bind(c => c
                        .ConnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName)),
                    NetworkChangeOperation.ConnectVMAdapters);
            }

            AddOperation(StartOVN, NetworkChangeOperation.StartOVN);

            return SuccessAff(default(OvsBridgesInfo));
        }
    }

    public Aff<RT, OvsBridgesInfo> RemoveOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OvsBridgesInfo currentBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RemoveOverlaySwitch)))
        {

            if (overlayVMAdapters.Length > 0)
            {
                _logger.LogDebug("Found adapters on overlay switch. Adding disconnect operations.");

                AddOperation(
                    () => default(RT).HostNetworkCommands.Bind(c => c.DisconnectNetworkAdapters(overlayVMAdapters)),
                    o => o!.Contains(NetworkChangeOperation.RemoveOverlaySwitch),
                    () => default(RT).HostNetworkCommands.Bind(c => c.ReconnectNetworkAdapters(overlayVMAdapters)),
                    NetworkChangeOperation.DisconnectVMAdapters);
            }

            AddOperation(StopOVN, _ => true, StartOVN, NetworkChangeOperation.StopOVN);

            foreach (var bridgeName in currentBridges.Bridges.Keys)
            {
                _logger.LogDebug("Adding operation to remove bridge {bridge}", bridgeName);

                AddOperation(
                    () => default(RT).OVS.Bind(o =>
                    {
                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        return o.RemoveBridge(bridgeName, cancelSourceCommand.Token).ToAff(e => e);
                    }),
                    NetworkChangeOperation.RemoveBridge, bridgeName);
            }

            AddOperation(
                () => default(RT).HostNetworkCommands.Bind(c => c.RemoveOverlaySwitch()),
                NetworkChangeOperation.RemoveOverlaySwitch);

            return SuccessAff(default(OvsBridgesInfo));
        }
    }

    public Aff<RT, Unit> EnableSwitchExtension(Guid switchId, string switchName)
    {
        using (_logger.BeginScope("Method: {method}", nameof(EnableSwitchExtension)))
        {
            AddOperation(
                () => default(RT).HostNetworkCommands.Bind(c => c
                    .EnableSwitchExtension(switchId)),
                NetworkChangeOperation.EnableSwitchExtension,
                switchName);

            return unitAff;
        }
    }

    public Aff<RT, Unit> DisableSwitchExtension(Guid switchId, string switchName)
    {
        using (_logger.BeginScope("Method: {method}", nameof(DisableSwitchExtension)))
        {
            AddOperation(
                () => default(RT).HostNetworkCommands.Bind(c => c
                    .DisableSwitchExtension(switchId)),
                NetworkChangeOperation.DisableSwitchExtension,
                switchName);

            return unitAff;
        }
    }

    public Aff<RT, OvsBridgesInfo> RemoveUnusedBridges(
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> newBridges)
    {
        using (_logger.BeginScope("Method: {Method}", nameof(RemoveUnusedBridges)))
        {
            var newBridgesNames = toHashSet(newBridges.Map(x => x.BridgeName));
            ovsBridges.Bridges.Values
                .Filter(bridge => bridge.Name != "br-int")
                .Filter(bridge => !newBridgesNames.Contains(bridge.Name))
                .Iter(bridge =>
                {
                    _logger.LogDebug("Adding operations to remove unused bridge {Bridge}", bridge);

                    AddOperation(() => default(RT).OVS.Bind(ovs =>
                        {
                            var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            return ovs.RemoveBridge(bridge.Name, cancelSourceCommand.Token).ToAff(e => e);
                        }),
                        NetworkChangeOperation.RemoveUnusedBridge, bridge);

                    ovsBridges = ovsBridges.RemoveBridge(bridge.Name);
                });

            return SuccessAff(ovsBridges);
        }
    }

    private (int? VlanTag, string? VlanMode) GetBridgePortSettings(NetworkProviderBridgeOptions? options)
    {
        var bridgeVlanTag = options?.BridgeVlan;
        var vlanMode = options?.VLanMode switch
        {
            BridgeVlanMode.Invalid => null,
            BridgeVlanMode.Access => "access",
            BridgeVlanMode.NativeUntagged => "native-untagged",
            BridgeVlanMode.NativeTagged => "native-tagged",
            null => null,
            _ => null
        };

        return (bridgeVlanTag, vlanMode);
    }

    public Aff<RT, Seq<string>> AddMissingBridges(
        bool hadSwitchBefore,
        Seq<string> enableBridges,
        OvsBridgesInfo ovsBridges,
        Seq<NewBridge> newBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(AddMissingBridges)))
        {

            var res = newBridges.Map(newBridge =>
            {
                if (hadSwitchBefore && ovsBridges.Bridges.Find(newBridge.BridgeName).IsSome)
                    return None;

                _logger.LogDebug("Adding operation to add bridge {bridge}", newBridge);

                var (vlanTag, vlanMode) = GetBridgePortSettings(newBridge.Options);

                AddOperation(() =>
                    from c in default(RT).HostNetworkCommands
                    from ovs in default(RT).OVS
                    let cancelAddBridge = new CancellationTokenSource(TimeSpan.FromSeconds(30))
                    from uAddBridge in ovs.AddBridge(newBridge.BridgeName, cancelAddBridge.Token).ToAff(l => l)
                    let cancelSetBridge = new CancellationTokenSource(TimeSpan.FromSeconds(30))
                    from uSetBridgePort in ovs
                        .UpdateBridgePort(newBridge.BridgeName, vlanTag, vlanMode, cancelSetBridge.Token).ToAff(l => l)
                    from uWait in c.WaitForBridgeAdapter(newBridge.BridgeName)
                    from uEnable in
                        enableBridges.Contains(newBridge.BridgeName)
                            ? c.EnableBridgeAdapter(newBridge.BridgeName)
                            : unitAff
                    select Unit.Default, NetworkChangeOperation.AddBridge, newBridge.BridgeName);

                return Some(newBridge.BridgeName);

            }).Somes().Strict();

            return SuccessAff(res);
        }
    }

    public Aff<RT, Seq<string>> RemoveInvalidNats(
        Seq<NetNat> netNat,
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges) =>
        from _ in unitAff
        from result in use(
            Eff(fun(() => _logger.BeginScope("Method: {method}", nameof(RemoveInvalidNats)))),
            _ => netNat.Filter(n => n.Name.StartsWith("eryph_"))
                .Map(n => RemoveInvalidNat(n, newConfig, newBridges))
                .SequenceSerial())
        // Force enumeration
        select result.Somes().ToArray().ToSeq();

    private Aff<RT, Option<string>> RemoveInvalidNat(
        NetNat nat,
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges) =>
        from _ in unitAff
        let providerConfig = newConfig.NetworkProviders
            .Find(p => GetNetNatName(p.Name) == nat.Name && p.Type == NetworkProviderType.NatOverLay)
        // When the prefix of the NetNat is invalid, we will just recreate the NetNat.
        let natPrefix = Try(() => IPNetwork2.Parse(nat.InternalIPInterfaceAddressPrefix))
            .ToOption()
        let bridge = providerConfig.Bind(p => newBridges.Find(b => b.BridgeName == p.BridgeName))
        let isNatValid = bridge.Map(b => b.Network == natPrefix).IfNone(false)
        from result in isNatValid
            ? SuccessAff(Option<string>.None)
            : from _1 in unitAff
              from _2 in Eff(fun(() => _logger.LogDebug("Removing invalid host NAT '{Nat}'", nat.Name)))
              let _3 = AddOperation(
                  () => default(RT).HostNetworkCommands.Bind(c => c.RemoveNetNat(nat.Name)),
                  NetworkChangeOperation.RemoveNetNat,
                  nat.Name)
              select Some(nat.Name)
        select result;


    public Aff<RT, OvsBridgesInfo> RemoveAdapterPortsOnNatOverlays(
        NetworkProvidersConfiguration newConfig,
        Seq<HostNetworkAdapter> adapters,
        OvsBridgesInfo ovsBridges) =>
        use(BeginScope(nameof(RemoveAdapterPortsOnNatOverlays)), _ =>
            from changedBridges in newConfig.NetworkProviders.ToSeq()
                .Filter(np => np.Type is NetworkProviderType.NatOverLay)
                .Map(np => ovsBridges.Bridges.Find(np.BridgeName))
                .Somes()
                .Map(b => RemoveAdapterPortsOnNatOverlay(b, adapters))
                .SequenceSerial()
            let result = changedBridges
                .Fold(ovsBridges, (s, b) => s.SetBridge(b))
            select result);

    public Aff<RT, OvsBridgeInfo> RemoveAdapterPortsOnNatOverlay(
        OvsBridgeInfo ovsBridge,
        Seq<HostNetworkAdapter> adapters) =>
        from _1 in unitAff
        let portsToRemove = adapters.Map(a => a.Name)
            // We also need to remove the bonded port if it exists
            .Append(GetBondPortName(ovsBridge.Name))
            .Map(ovsBridge.Ports.Find)
            .Somes()
        from _2 in portsToRemove.Map(port =>
        {
            _logger.LogDebug("Adapter {Adapter} found on host nat bridge {Bridge}. Removing it from the bridge.",
                port.PortName, ovsBridge.Name);

            AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS.ToAff()
                    from ct in cancelToken<RT>().ToAff()
                    from _ in ovs.RemovePort(port.BridgeName, port.PortName, ct).ToAff(l => l)
                    select unit),
                NetworkChangeOperation.RemoveAdapterPort,
                port.PortName, port.BridgeName);
            return unitAff;
        }).SequenceSerial()
        select ovsBridge.RemovePorts(portsToRemove.Map(p => p.PortName).ToSeq());

    public Aff<RT, Unit> ConfigureNatAdapters(
        NetworkProvidersConfiguration newConfig,
        Seq<NetNat> netNat,
        Seq<string> createdBridges,
        Seq<NewBridge> newBridges,
        Seq<string> removedNats)
    {
        using (_logger.BeginScope("Method: {method}", nameof(ConfigureNatAdapters)))
        {

            return default(RT).HostNetworkCommands.Bind(c =>
            {
                var res = from networkProvider in newConfig.NetworkProviders
                        .Where(x => x.Type is NetworkProviderType.NatOverLay)

                          from newBridge in newBridges
                              .Find(x => x.BridgeName == networkProvider.BridgeName)

                          let isNewCreatedBridge = createdBridges.Contains(newBridge.BridgeName)
                          let newNatName = GetNetNatName(networkProvider.Name)

                          select from updateBridgeAdapter in !isNewCreatedBridge
                                  ? c.GetAdapterIpV4Address(newBridge.BridgeName)
                                      .Map(ips =>
                                      {
                                          if (ips.Length != 1)
                                              return true;

                                          var currentIp = ips[0];
                                          var res = currentIp.PrefixLength == newBridge.Network.Cidr &&
                                                 currentIp.IPAddress == newBridge.IPAddress.ToString();

                                          if (!res)
                                          {
                                              _logger.LogDebug("host nat adapter {bridgeName} has invalid ip. Expected: {expectedIp}/{expectedSuffix}, Actual: {actualIp}/{actualSuffix}",
                                                      networkProvider.BridgeName, newBridge.IPAddress, newBridge.Network.Cidr,
                                                      currentIp.IPAddress, currentIp.PrefixLength);

                                          }

                                          return !res;

                                      })
                                  : SuccessAff(true)

                                 let _ = updateBridgeAdapter
                                     ? AddOperation(
                                         () => default(RT).HostNetworkCommands.Bind(cc => cc
                                             .EnableBridgeAdapter(newBridge.BridgeName)
                                             .Bind(_ => c.ConfigureAdapterIp(newBridge.BridgeName, newBridge.IPAddress,
                                                 newBridge.Network))), NetworkChangeOperation.ConfigureNatIp,
                                         newBridge.BridgeName)
                                     : unit

                                 let __ = netNat.Find(n => n.Name == newNatName)
                                     .IfNone(() =>
                                     {
                                         AddOperation(
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .AddNetNat(newNatName, newBridge.Network)),
                                             _ => true,
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .RemoveNetNat(newNatName)),
                                             NetworkChangeOperation.AddNetNat, newNatName, newBridge.Network);
                                     })

                                 let ___ = removedNats.Find(n => n == newNatName)
                                     .IfSome(_ =>
                                     {
                                         AddOperation(
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .AddNetNat(newNatName, newBridge.Network)),
                                             _ => true,
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .RemoveNetNat(newNatName)),
                                             NetworkChangeOperation.AddNetNat, newNatName, newBridge.Network);
                                     })

                                 select unit;

                return res.ToArray() //force enumeration to generate updates
                    .SequenceSerial();
            }).Map(_ => unit);
        }
    }

    /// <summary>
    /// This method removes OVS bridges for which the host network adapter no longer exist.
    /// The bridges will be recreated later.
    /// </summary>
    //TODO Should we do that for both overlay and nat overlay?
    public Aff<RT, OvsBridgesInfo> RecreateMissingNatAdapters(
        NetworkProvidersConfiguration newConfig,
        Seq<string> adapterNames,
        OvsBridgesInfo ovsBridges) =>
        use(BeginScope(nameof(RecreateMissingNatAdapters)), _ => 
            from _1 in unitAff
            let bridgesToRemove = newConfig.NetworkProviders.ToSeq()
                .Filter(np => np.Type is NetworkProviderType.NatOverLay)
                //.Where(np => x.BridgeName != null)
                .Filter(np => ovsBridges.Bridges.Find(np.BridgeName).IsSome)
                .Filter(np => !adapterNames.Contains(np.BridgeName))
                .Map(np => np.BridgeName)
            from _2 in bridgesToRemove
                .Map(bridgeName =>
                {
                    _logger.LogWarning("Adapter for NAT bridge {Bridge} not found. Recreating bridge.",
                        bridgeName);
                    AddOperation(
                        () => timeout(
                            TimeSpan.FromSeconds(30),
                            from ovs in default(RT).OVS.ToAff()
                            from ct in cancelToken<RT>().ToAff()
                            from _ in ovs.RemoveBridge(bridgeName, ct).ToAff(e => e)
                            select unit),
                        NetworkChangeOperation.RemoveMissingBridge,
                        bridgeName);
                    return unitAff;
                }).SequenceSerial()
            let result = bridgesToRemove
                .Fold(ovsBridges, (s, b) => s.RemoveBridge(b))
            select result);

    public Aff<RT, Unit> UpdateBridgePorts(
        NetworkProvidersConfiguration newConfig,
        Seq<string> createdBridges,
        OvsBridgesInfo ovsBridges) =>
        use(BeginScope(nameof(UpdateBridgePorts)), _ =>
            from _1 in newConfig.NetworkProviders.ToSeq()
                .Filter(np => np.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverLay)
                .Filter(np => !createdBridges.Contains(np.BridgeName))
                .Map(np => UpdateBridgePort(np, ovsBridges))
                .SequenceSerial()
            select unit);

    private Aff<RT, Unit> UpdateBridgePort(
        NetworkProvider providerConfig,
        OvsBridgesInfo ovsBridgesInfo) =>
        from ovsBridgeInfo in ovsBridgesInfo.Bridges.Find(providerConfig.BridgeName)
            .ToAff(Error.New(
                $"Could not update port of existing bridge '{providerConfig.BridgeName}'. The bridge does not exist."))
        // TODO is this correct? We use find with both bridge name and port name
        from ovsBridgePort in ovsBridgeInfo.Ports.Find(providerConfig.BridgeName)
            .ToAff(Error.New(
                $"Could not update port of existing bridge '{providerConfig.BridgeName}'. The port does not exist."))
        let expectedPortSettings = GetBridgePortSettings(providerConfig.BridgeOptions)
        let currentPortSettings = (ovsBridgePort.Tag, ovsBridgePort.VlanMode)
        let _ = currentPortSettings.Tag.GetValueOrDefault() == expectedPortSettings.VlanTag.GetValueOrDefault()
                && currentPortSettings.VlanMode == expectedPortSettings.VlanMode
            ? unit
            : AddOperation(() => default(RT).OVS.Bind(ovs =>
                {
                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    return ovs.UpdateBridgePort(providerConfig.BridgeName, expectedPortSettings.VlanTag, expectedPortSettings.VlanMode, cancelSourceCommand.Token)
                        .ToAff(l => l);
                }),
                _ => true,
                () => default(RT).OVS.Bind(ovs =>
                {
                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    return ovs
                        .UpdateBridgePort(providerConfig.BridgeName, currentPortSettings.Tag , currentPortSettings.VlanMode, cancelSourceCommand.Token)
                        .ToAff(l => l);
                }),
                NetworkChangeOperation.UpdateBridgePort, providerConfig.BridgeName)
        select unit;

    // TODO interface might move from one bond to another
    // 1. Remove all ports which contain incorrect adapters
    // 2. Recreate all ports
    // Assumption: each physical adapter is used for exactly one bridge!
    // TODO Remove port when no adapters are specified but physical adapters are attacheds

    // TODO there is special case where the adapter is connected to a different bridge -> remove from old bridge add to new bridge

    public Aff<RT, Unit> CreateOverlayAdapterPorts(
        NetworkProvidersConfiguration newConfig,
        OvsBridgesInfo ovsBridges) =>
        use(BeginScope(nameof(CreateOverlayAdapterPorts)), _ =>
            from _1 in newConfig.NetworkProviders.ToSeq()
                .Filter(x => x.Type is NetworkProviderType.Overlay)
                .Filter(x => x.Adapters is { Length: > 0 })
                .Map(np => CreateOverlayAdapterPort(np, ovsBridges))
                .SequenceSerial()
            select unit);

    private Aff<RT, Unit> CreateOverlayAdapterPort(
        NetworkProvider providerConfig,
        OvsBridgesInfo ovsBridges) =>
        from _1 in unitAff
        let expectedAdapters = toHashSet(providerConfig.Adapters)
        let expectedPortName = expectedAdapters.Count > 1
            ? GetBondPortName(providerConfig.BridgeName)
            : providerConfig.Adapters[0]
        let outdatedExternalPorts = ovsBridges.Bridges
            .Find(providerConfig.BridgeName).ToSeq()
            .Bind(b => b.Ports.Values)
            .Filter(p => p.Interfaces.Exists(i => i.IsExternal))
            .Filter(p => toHashSet(p.Interfaces.Map(i => i.Name)) != expectedAdapters || p.PortName != expectedPortName)
        from _2 in outdatedExternalPorts
            .Map(RemoveOverlayAdapterPort)
            .SequenceSerial()
        let existingPort = ovsBridges.Bridges
            .Find(providerConfig.BridgeName)
            .Bind(b => b.Ports.Find(expectedPortName))
            .Filter(p => toHashSet(p.Interfaces.Map(i => i.Name)) == expectedAdapters)
        // When the provider has no adapters configured at all, we dot not need to create
        // or update the overlay port.
        from _4 in expectedAdapters.Count > 0
            ? existingPort.Match(
                None: () => AddOverlayAdapterPort(providerConfig, expectedPortName),
                Some: portInfo => UpdateOverlayAdapterPort(providerConfig, portInfo))
            : unitAff
        select unit;
    
    private Aff<RT, Unit> AddOverlayAdapterPort(
        NetworkProvider providerConfig,
        string expectedPortName) =>
        from _1 in unitAff
        let adapters = providerConfig.Adapters.ToSeq().Strict()
        let ovsBondMode = GetBondMode(providerConfig.BridgeOptions?.BondMode)
        let _2 = AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in  adapters.Length > 1
                    ? ovs.AddBond(providerConfig.BridgeName, expectedPortName, adapters, ovsBondMode, ct).ToAff(e => e)
                    : ovs.AddPort(providerConfig.BridgeName, expectedPortName, ct).ToAff(e => e)
                select unit),
            _ => true,
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(providerConfig.BridgeName, expectedPortName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.AddBondPort,
            providerConfig.BridgeName, expectedPortName)
        select unit;

    private Aff<RT, Unit> RemoveOverlayAdapterPort(
        OvsBridgePortInfo portInfo) =>
        from _1 in unitAff
        // TODO add rollback
        let _2 = AddOperation(
            () => timeout(
                TimeSpan.FromSeconds(30),
                from ovs in default(RT).OVS
                from ct in cancelToken<RT>()
                from _ in ovs.RemovePort(portInfo.BridgeName, portInfo.PortName, ct).ToAff(e => e)
                select unit),
            NetworkChangeOperation.RemoveAdapterPort,
            portInfo.PortName, portInfo.BridgeName)
        select unit;

    private Aff<RT, Unit> UpdateOverlayAdapterPort(
        NetworkProvider providerConfig,
        OvsBridgePortInfo portInfo) =>
        from _1 in unitAff
        let expectedBondMode = GetBondMode(providerConfig.BridgeOptions?.BondMode)
        let _2 = portInfo.BondMode == expectedBondMode
            ? unit
            : AddOperation(
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    // TODO does an update by name even work or do we need the ID?
                    from _ in ovs.UpdateBondPort(portInfo.PortName, expectedBondMode, ct).ToAff(e => e)
                    select unit),
                _ => true,
                () => timeout(
                    TimeSpan.FromSeconds(30),
                    from ovs in default(RT).OVS
                    from ct in cancelToken<RT>()
                    // TODO does an update by name even work or do we need the ID?
                    // We force a bond mode during the rollback as no bond mode at all
                    // can break the physical network. When bond port is configured,
                    // the bond mode should by set anyways.
                    from _ in ovs.UpdateBondPort(portInfo.PortName, portInfo.BondMode ?? "active-backup", ct).ToAff(e => e)
                    select unit),
                NetworkChangeOperation.UpdateBridgePort,
                providerConfig.BridgeName, portInfo.PortName)
        select unit;

    public Aff<RT, Unit> UpdateBridgeMappings(NetworkProvidersConfiguration newConfig)
    {
        using (_logger.BeginScope("Method: {method}", nameof(UpdateBridgeMappings)))
        {
            var cancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            return from ovs in default(RT).OVS
                   let bridgeMappings = string.Join(',', newConfig.NetworkProviders
                       .Where(x => !string.IsNullOrWhiteSpace(x.BridgeName))
                       .Map(networkProvider => $"{networkProvider.Name}:{networkProvider.BridgeName}"))
                   
                   from ovsTable in ovs.GetOVSTable(cancelSource.Token).ToAff(l => l)
                   
                   let _ = !ovsTable.ExternalIds.ContainsKey("ovn-bridge-mappings") ||
                           ovsTable.ExternalIds["ovn-bridge-mappings"] != bridgeMappings

                       ? AddOperation(() =>
                           default(RT).OVS.Bind(ovsC =>
                           {
                               var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                               return ovsC.UpdateBridgeMapping(bridgeMappings, cancelSourceCommand.Token)
                                   .ToAff(l => l);
                           }), NetworkChangeOperation.UpdateBridgeMapping)
                       : Unit.Default

                   select Unit.Default;

        }
    }

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

    public NetworkChanges<RT> Build()
    {
        return new NetworkChanges<RT>() { Operations = _operations };
    }
}