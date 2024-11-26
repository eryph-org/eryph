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
using LanguageExt.Effects.Traits;
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

    public Aff<RT, OVSBridgeInfo> RebuildOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OVSBridgeInfo ovsBridges,
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

            foreach (var bridge in ovsBridges.Bridges)
            {
                _logger.LogDebug("Adding operation to remove bridge {bridge}", bridge);

                AddOperation(
                    () => default(RT).OVS.Bind(o =>
                    {
                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        return o.RemoveBridge(bridge, cancelSourceCommand.Token).ToAff(e => e);
                    }),
                    NetworkChangeOperation.RemoveBridge, bridge);

                ovsBridges = ovsBridges with { Bridges = ovsBridges.Bridges.Remove(bridge) };

                ovsBridges.BridgePorts.Filter(x => x.Value == bridge)
                    .Iter(bp =>
                    {
                        ovsBridges = ovsBridges with
                        {
                            BridgePorts = ovsBridges.BridgePorts.Remove(bp.Key)
                        };
                    });

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

            return SuccessAff(default(OVSBridgeInfo));
        }
    }

    public Aff<RT, OVSBridgeInfo> RemoveOverlaySwitch(
        Seq<TypedPsObject<VMNetworkAdapter>> overlayVMAdapters,
        OVSBridgeInfo currentBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RemoveOverlaySwitch)))
        {

            if (overlayVMAdapters.Length > 0)
            {
                _logger.LogDebug("Found adapters on overlay switch. Adding disconnect operations.");

                AddOperation(
                    () => default(RT).HostNetworkCommands.Bind(c => c
                        .DisconnectNetworkAdapters(overlayVMAdapters)),
                    o =>
                        o!.Contains(NetworkChangeOperation.RemoveOverlaySwitch),
                    () => default(RT).HostNetworkCommands.Bind(c => c
                        .ReconnectNetworkAdapters(overlayVMAdapters)),
                    NetworkChangeOperation.DisconnectVMAdapters);
            }

            AddOperation(StopOVN, _ => true, StartOVN, NetworkChangeOperation.StopOVN);

            AddOperation(
                () => default(RT).OVS.Bind(ovs =>
                {
                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    return ovs.RemoveBridge("br-int", cancelSourceCommand.Token).ToAff(e => e);
                }),
                NetworkChangeOperation.RemoveBridge, "br-int");

            _logger.LogDebug("Adding operations to remove all bridges.");

            AddOperation(() =>
                    default(RT).OVS.Bind(ovs => currentBridges.Bridges.Map(b =>
                        {
                            var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            return ovs.RemoveBridge(b, cancelSourceCommand.Token);
                        })
                        .TraverseSerial(l => l).Map(_ => Unit.Default)
                        .ToAff(e => e.Message)
                        .Bind(_ => default(RT).HostNetworkCommands.Bind(c => c
                            .RemoveOverlaySwitch()))),
                NetworkChangeOperation.RemoveOverlaySwitch);

            return SuccessAff(default(OVSBridgeInfo));
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

    public Aff<RT, OVSBridgeInfo> RemoveUnusedBridges(OVSBridgeInfo ovsBridges, Seq<NewBridge> newBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RemoveUnusedBridges)))
        {

            ovsBridges.Bridges
                .Where(bridge => bridge != "br-int")
                .Where(bridge => newBridges
                    .All(x => x.BridgeName != bridge))
                .Iter(bridge =>
                {
                    _logger.LogDebug("Adding operations to remove unused bridge {bridge}", bridge);

                    AddOperation(() => default(RT).OVS.Bind(ovs =>
                        {
                            var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            return ovs.RemoveBridge(bridge, cancelSourceCommand.Token).ToAff(e => e);
                        }),
                        NetworkChangeOperation.RemoveUnusedBridge, bridge);

                    ovsBridges = ovsBridges with { Bridges = ovsBridges.Bridges.Remove(bridge) };

                    ovsBridges.BridgePorts.Filter(x => x.Value == bridge)
                        .Iter(bp =>
                        {
                            ovsBridges = ovsBridges with
                            {
                                BridgePorts = ovsBridges.BridgePorts.Remove(bp.Key)
                            };
                        });
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
        OVSBridgeInfo ovsBridges,
        Seq<NewBridge> newBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(AddMissingBridges)))
        {

            var res = newBridges.Map(newBridge =>
                {

                    if (hadSwitchBefore && ovsBridges.Bridges.Contains(newBridge.BridgeName))
                        return Array.Empty<string>();

                    _logger.LogDebug("Adding operation to add bridge {bridge}", newBridge);

                    var (vlanTag, vlanMode) = GetBridgePortSettings(newBridge.Options);

                    AddOperation(() =>
                        from c in default(RT).HostNetworkCommands
                        from ovs in default(RT).OVS
                        let cancelAddBridge = new CancellationTokenSource(TimeSpan.FromSeconds(30))
                        from uAddBridge in ovs.AddBridge(newBridge.BridgeName, cancelAddBridge.Token).ToAff(l => l)
                        let cancelSetBridge = new CancellationTokenSource(TimeSpan.FromSeconds(30))
                        from uSetBridgePort in ovs.UpdateBridgePort(newBridge.BridgeName, vlanTag, vlanMode, cancelSetBridge.Token).ToAff(l => l)
                        from uWait in c.WaitForBridgeAdapter(newBridge.BridgeName)
                        from uEnable in
                            enableBridges.Contains(newBridge.BridgeName)
                             ? c.EnableBridgeAdapter(newBridge.BridgeName)
                             : unitAff
                        select Unit.Default, NetworkChangeOperation.AddBridge, newBridge.BridgeName);

                    return new[] { newBridge.BridgeName };

                }).Flatten().ToArray() //force enumeration
                .ToSeq();

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


    public Aff<RT, OVSBridgeInfo> RemoveAdapterPortsOnNatOverlays(
        NetworkProvidersConfiguration newConfig,
        Seq<HostNetworkAdapter> adapters,
        OVSBridgeInfo ovsBridges)

    {
        using (_logger.BeginScope("Method: {method}", nameof(RemoveAdapterPortsOnNatOverlays)))
        {


            foreach (var networkProvider in newConfig.NetworkProviders
                         .Where(x => x.Type is NetworkProviderType.NatOverLay))
            {
                foreach (var adapter in adapters)
                {
                    ovsBridges.BridgePorts.Find(adapter.Name).IfSome(bp =>
                    {
                        if (bp != networkProvider.BridgeName) return;

                        _logger.LogDebug("Adapter {adapter} found on host nat bridge {bridge}. Removing it from the bridge",
                            adapter.Name, networkProvider.BridgeName);


                        AddOperation(
                            () => default(RT).OVS.Bind(ovs =>
                            {
                                var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                return ovs.RemovePort(networkProvider.BridgeName, adapter.Name, cancelSourceCommand.Token)
                                    .ToAff(l => l);
                            }), NetworkChangeOperation.RemoveAdapterPort,
                            adapter.Name, networkProvider.BridgeName);

                        ovsBridges = ovsBridges with
                        {
                            BridgePorts = ovsBridges.BridgePorts.Remove(adapter.Name)
                        };
                    });
                }
            }

            return SuccessAff(ovsBridges);
        }
    }

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

    public Aff<RT, OVSBridgeInfo> RecreateMissingNatAdapters(
        NetworkProvidersConfiguration newConfig,
        Seq<string> adapterNames, OVSBridgeInfo ovsBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RecreateMissingNatAdapters)))
        {

            return default(RT).HostNetworkCommands.Bind(c =>
            {
                var res =
                    from networkProvider in newConfig.NetworkProviders
                        .Where(x => x.Type is NetworkProviderType.NatOverLay)
                        .Where(x => x.BridgeName != null)
                        .Where(x => ovsBridges.Bridges.Contains(x.BridgeName))
                        .Where(x => !adapterNames.Contains(x.BridgeName))
                        .Map(provider =>
                        {
                            _logger.LogWarning("Adapter for nat bridge {bridge} not found. Recreating bridge.",
                                provider.BridgeName);

                            AddOperation(() => default(RT).OVS.Bind(ovs =>
                                {
                                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    return ovs.RemoveBridge(provider.BridgeName, cancelSourceCommand.Token).ToAff(e => e);
                                }),
                                NetworkChangeOperation.RemoveMissingBridge, provider.BridgeName);

                            ovsBridges = ovsBridges with { Bridges = ovsBridges.Bridges.Remove(provider.BridgeName) };

                            ovsBridges.BridgePorts.Filter(x => x.Value == provider.BridgeName)
                                .Iter(bp =>
                                {
                                    ovsBridges = ovsBridges with
                                    {
                                        BridgePorts = ovsBridges.BridgePorts.Remove(bp.Key)
                                    };
                                });

                            return unitAff;
                        })
                    select networkProvider;

                return res.ToArray() //force enumeration to generate updates
                    .SequenceSerial();
            }).Map(_ => ovsBridges);
        }
    }

    public Aff<RT, Unit> UpdateBridgePorts(
        NetworkProvidersConfiguration newConfig,
        Seq<string> createdBridges,
        OVSBridgeInfo ovsBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(UpdateBridgePorts)))
        {

            foreach (var networkProvider in newConfig.NetworkProviders
                         .Where(x => x.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverLay))
            {
                var (vlanTag, vlanMode) = GetBridgePortSettings(networkProvider.BridgeOptions);

                if (createdBridges.Contains(networkProvider.BridgeName))
                    return SuccessAff(unit);

                var (currentTag, currentVLanMode) = ovsBridges.Ports.Find(networkProvider.BridgeName)
                    .Map(port => (port.Tag, port.VlanMode)).IfNone((null,null));

                if(currentTag.GetValueOrDefault() == vlanTag.GetValueOrDefault() && currentVLanMode == vlanMode)
                    return SuccessAff(unit);


                AddOperation(
                    () => default(RT).OVS.Bind(ovs =>
                    {
                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                        return ovs.UpdateBridgePort(networkProvider.BridgeName, vlanTag, vlanMode, cancelSourceCommand.Token)
                            .ToAff(l => l);
                    }),
                    _ => true,
                    () => default(RT).OVS.Bind(ovs =>
                    {
                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        return ovs
                            .UpdateBridgePort(networkProvider.BridgeName, currentTag, currentVLanMode, cancelSourceCommand.Token)
                            .ToAff(l => l);
                    }),
                    NetworkChangeOperation.UpdateBridgePort, networkProvider.BridgeName
                );

            }

            return SuccessAff(unit);
        }
    }

    public Aff<RT, Unit> CreateOverlayAdapterPorts(
        NetworkProvidersConfiguration newConfig,
        OVSBridgeInfo ovsBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(CreateOverlayAdapterPorts)))
        {

            foreach (var networkProvider in newConfig.NetworkProviders
                         .Where(x => x.Type is NetworkProviderType.Overlay)
                         .Where(x => x.Adapters != null))
            {
                foreach (var adapterName in networkProvider.Adapters)
                {
                    _ = ovsBridges.BridgePorts.Find(adapterName).Match(
                        None: () =>
                        {
                            _logger.LogDebug("Adding {adapter} to bridge {bridge}", adapterName, networkProvider.BridgeName);
                            return AddOperation(
                                () => default(RT).OVS.Bind(ovs =>
                                {
                                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                                    return ovs.AddPort(networkProvider.BridgeName, adapterName, cancelSourceCommand.Token)
                                        .ToAff(l => l);
                                }),
                                _ => true,
                                () => default(RT).OVS.Bind(ovs =>
                                {
                                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    return ovs
                                        .RemovePort(networkProvider.BridgeName, adapterName, cancelSourceCommand.Token)
                                        .ToAff(l => l);
                                }),
                                NetworkChangeOperation.AddAdapterPort, adapterName, networkProvider.BridgeName
                            );
                        },

                        Some:
                        portBridge =>
                        {
                            if (portBridge == networkProvider.BridgeName)
                                return Unit.Default;

                            AddOperation(
                                () => default(RT).OVS.Bind(ovs =>
                                {
                                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    return ovs.RemovePort(portBridge, adapterName, cancelSourceCommand.Token).ToAff(l => l);
                                }),
                                _ => true,
                                () => default(RT).OVS.Bind(ovs =>
                                {
                                    var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                    return ovs.AddPort(portBridge, adapterName, cancelSourceCommand.Token).ToAff(l => l);
                                }),
                                NetworkChangeOperation.RemoveAdapterPort, adapterName, portBridge
                            );

                            return AddOperation(
                                () => default(RT).OVS.Bind(
                                    ovs =>
                                    {
                                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                        return ovs.AddPort(networkProvider.BridgeName, adapterName, cancelSourceCommand.Token)
                                            .ToAff(l => l);
                                    }),
                                _ => true,
                                () => default(RT).OVS.Bind(
                                    ovs =>
                                    {
                                        var cancelSourceCommand = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                        return ovs.RemovePort(networkProvider.BridgeName, adapterName, cancelSourceCommand.Token)
                                            .ToAff(l => l);
                                    }),
                                NetworkChangeOperation.AddAdapterPort, adapterName, networkProvider.BridgeName
                            );
                        }
                    );
                }

            }

            return unitAff;
        }
    }

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

    public NetworkChanges<RT> Build()
    {
        return new NetworkChanges<RT>() { Operations = _operations };
    }
}