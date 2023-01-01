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

    ConfigureNatIp,
    UpdateBridgeMapping
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

    private static Aff<RT, Unit> RebuildNetworks()
    {
        var cts = new CancellationTokenSource(2000);

        return default(RT).AgentSync.Bind(c => c
            .SendSyncCommand("REBUILD_NETWORKS", cts.Token));

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
                        .ReconnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName))
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
                        o.RemoveBridge(bridge).ToAff(e => e)),
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
                        .ReconnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName)),
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
                        .ReconnectNetworkAdapters(overlayVMAdapters, EryphConstants.OverlaySwitchName)),
                    NetworkChangeOperation.DisconnectVMAdapters);
            }

            AddOperation(StopOVN, _ => true, StartOVN, NetworkChangeOperation.StopOVN);

            AddOperation(
                () => default(RT).OVS.Bind(ovs =>
                    ovs.RemoveBridge("br-int").ToAff(e => e)),
                NetworkChangeOperation.RemoveBridge, "br-int");

            _logger.LogDebug("Adding operations to remove all bridges.");

            AddOperation(() =>
                    default(RT).OVS.Bind(ovs =>
                        currentBridges.Bridges.Map(b => ovs.RemoveBridge(b))
                            .TraverseSerial(l => l).Map(_ => Unit.Default)
                            .ToAff(e => e.Message)
                            .Bind(_ => default(RT).HostNetworkCommands.Bind(c => c
                                .RemoveOverlaySwitch()))),
                NetworkChangeOperation.RemoveOverlaySwitch);

            return SuccessAff(default(OVSBridgeInfo));
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
                            ovs.RemoveBridge(bridge).ToAff(e => e)),
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

                    AddOperation(() =>
                        from c in default(RT).HostNetworkCommands
                        from ovs in default(RT).OVS
                        from uAddBridge in ovs.AddBridge(newBridge.BridgeName).ToAff(l => l)
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

    public Aff<RT, Unit> RemoveUnusedNat(
        Seq<NetNat> netNat,
        NetworkProvidersConfiguration newConfig,
        Seq<NewBridge> newBridges)
    {
        using (_logger.BeginScope("Method: {method}", nameof(RemoveUnusedNat)))
        {

            foreach (var nat in netNat.Filter(x => x.Name.StartsWith("eryph_")))
            {
                _logger.LogTrace("Checking host nat {nat}", nat.Name);


                var providerName = nat.Name["eryph_".Length..];
                newConfig.NetworkProviders.Find(x =>
                        x.Name == providerName && x.Type == NetworkProviderType.NatOverLay)
                    .Match(
                        None: () =>
                        {
                            _logger.LogDebug("Removing invalid host nat {nat}", nat.Name);

                            AddOperation(
                                () => default(RT).HostNetworkCommands.Bind(c => c
                                    .RemoveNetNat(nat.Name)),
                                NetworkChangeOperation.RemoveNetNat, nat.Name);

                            netNat = netNat.Filter(x => x.Name != nat.Name);
                        },
                        Some: provider =>
                        {
                            var network = IPNetwork.Parse(nat.InternalIPInterfaceAddressPrefix);

                            newBridges.Find(x => x.BridgeName == provider.BridgeName).IfSome(bridge =>
                            {
                                if (!network.Equals(bridge.Network))
                                {
                                    _logger.LogDebug("Removing invalid host nat {nat}", nat.Name);

                                    AddOperation(
                                        () => default(RT).HostNetworkCommands.Bind(c => c
                                            .RemoveNetNat(nat.Name)), NetworkChangeOperation.RemoveNetNat);

                                    netNat = netNat.Filter(x => x.Name != nat.Name);
                                }
                            });

                        }
                    );
            }

            return unitAff;
        }
    }

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
                                ovs.RemovePort(networkProvider.BridgeName, adapter.Name)
                                    .ToAff(l => l)), NetworkChangeOperation.RemoveAdapterPort,
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
        Seq<NewBridge> newBridges)
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

                          select from updateBridgeAdapter in !isNewCreatedBridge
                                  ? c.GetAdapterIpV4Address(newBridge.BridgeName)
                                      .Map(ips =>
                                      {
                                          if (ips.Length != 1)
                                              return true;

                                          var currentIp = ips[0];
                                          var res = currentIp.PrefixLength == newBridge.Network.Cidr &&
                                                 currentIp.IpAddress == newBridge.IPAddress.ToString();

                                          if (!res)
                                          {
                                              _logger.LogDebug("host nat adapter {bridgeName} has invalid ip. Expected: {expectedIp}/{expectedSuffix}, Actual: {actualIp}/{actualSuffix}",
                                                      networkProvider.BridgeName, newBridge.IPAddress, newBridge.Network.Cidr,
                                                      currentIp.IpAddress, currentIp.PrefixLength);

                                          }

                                          return !res;

                                      })
                                  : SuccessAff(true)

                                 let _ = updateBridgeAdapter
                                     ? AddOperation(
                                         () => default(RT).HostNetworkCommands.Bind(cc => cc
                                             .EnableBridgeAdapter(newBridge.BridgeName)
                                             .Bind(_ => c.ConfigureNATAdapter(newBridge.BridgeName, newBridge.IPAddress,
                                                 newBridge.Network))), NetworkChangeOperation.ConfigureNatIp,
                                         newBridge.BridgeName)
                                     : unit

                                 let __ = netNat.Find(n => n.Name == $"eryph_{networkProvider.Name}")
                                     .IfNone(() =>
                                     {
                                         AddOperation(
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .AddNetNat($"eryph_{networkProvider.Name}", newBridge.Network)),
                                             _ => true,
                                             () => default(RT).HostNetworkCommands.Bind(cc => cc
                                                 .RemoveNetNat($"eryph_{networkProvider.Name}")),
                                             NetworkChangeOperation.AddNetNat, newBridge.Network);
                                     })

                                 select unit;

                return res.ToArray() //force enumeration to generate updates
                    .TraverseParallel(l => l);
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
                                    ovs.RemoveBridge(provider.BridgeName).ToAff(e => e)),
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
                    .TraverseParallel(l => l);
            }).Map(_ => ovsBridges);
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
                                () => default(RT).OVS.Bind(ovs => ovs.AddPort(networkProvider.BridgeName, adapterName)
                                    .ToAff(l => l)),
                                _ => true,
                                () => default(RT).OVS.Bind(ovs => ovs
                                    .RemovePort(networkProvider.BridgeName, adapterName)
                                    .ToAff(l => l)),
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
                                    ovs.RemovePort(portBridge, adapterName).ToAff(l => l)),
                                _ => true,
                                () => default(RT).OVS.Bind(ovs =>
                                    ovs.AddPort(portBridge, adapterName).ToAff(l => l)),
                                NetworkChangeOperation.RemoveAdapterPort, adapterName, portBridge
                            );

                            return AddOperation(
                                () => default(RT).OVS.Bind(
                                    ovs => ovs.AddPort(networkProvider.BridgeName, adapterName)
                                        .ToAff(l => l)),
                                _ => true,
                                () => default(RT).OVS.Bind(
                                    ovs => ovs.RemovePort(networkProvider.BridgeName, adapterName)
                                        .ToAff(l => l)),
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
            var cancelSource = new CancellationTokenSource(5000);

            return from ovs in default(RT).OVS
                   let bridgeMappings = string.Join(',', newConfig.NetworkProviders
                       .Where(x => !string.IsNullOrWhiteSpace(x.BridgeName))
                       .Map(networkProvider => $"{networkProvider.Name}:{networkProvider.BridgeName}"))
                   
                   from ovsTable in ovs.GetOVSTable(cancelSource.Token).ToAff(l => l)
                   
                   let _ = !ovsTable.ExternalIds.ContainsKey("ovn-bridge-mappings") ||
                           ovsTable.ExternalIds["ovn-bridge-mappings"] != bridgeMappings

                       ? AddOperation(() =>
                           default(RT).OVS.Bind(ovsC =>
                               ovsC.UpdateBridgeMapping(bridgeMappings, CancellationToken.None)
                                   .ToAff(l => l)), NetworkChangeOperation.UpdateBridgeMapping)
                       : Unit.Default

                   select Unit.Default;

        }
    }


    public NetworkChanges<RT> Build()
    {
        return new NetworkChanges<RT>() { Operations = _operations };
    }
}