using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Networks
{
    [UsedImplicitly]
    internal class CreateProjectNetworksSaga : OperationTaskWorkflowSaga<CreateNetworksCommand, CreateProjectNetworksSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

    {
        private readonly IStateStore _stateStore;
        private readonly ILogger _log;
        private readonly INetworkProviderManager _networkProviderManager;

        public CreateProjectNetworksSaga(IBus bus, IOperationTaskDispatcher taskDispatcher,
            IStateStore stateStore, ILogger log, INetworkProviderManager networkProviderManager) : base(bus, taskDispatcher)
        {
            _stateStore = stateStore;
            _log = log;
            _networkProviderManager = networkProviderManager;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateProjectNetworksSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override async Task Initiated(CreateNetworksCommand message)
        {
            Data.Config = NormalizeConfig(message.Config);
            _log.LogTrace("Update project networks. Config: {@Config}", Data.Config);
            
            var project = await _stateStore.Read<Project>().GetBySpecAsync(
                new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, Data.Config.Project ?? "default"));

            if (project == null)
            {
                await Fail(new ErrorData { ErrorMessage = $"Project {Data.Config.Project} not found" });
                return;
            }

            var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
                .Match(
                    r=> r,
                    l =>
                    {
                        l.Throw();
                        return new NetworkProvidersConfiguration();
                    });

            var messages = ValidateConfig(providerConfig).ToArray();

            if (messages.Length == 0) 
                messages = await AsyncToArray(ValidateChanges(project.Id, providerConfig));

            if (messages.Length == 0)
            {
                await UpdateNetwork(project.Id, providerConfig);

                await StartNewTask(new UpdateNetworksCommand
                {
                    Projects = new[] { project.Id }
                });

                return;
            }

            foreach (var validationMessage in messages)
            {
                _log.LogDebug("network change validation error: {message}", validationMessage);
            }

            await Fail(new ErrorData { ErrorMessage = string.Join('\n', messages) });
        }

        private static ProjectNetworksConfig NormalizeConfig(ProjectNetworksConfig config)
        {

            foreach (var networkConfig in config.Networks)
            {

                networkConfig.Subnets ??= Array.Empty<NetworkSubnetConfig>();

                foreach (var subnetConfig in networkConfig.Subnets)
                {
                    if(string.IsNullOrWhiteSpace(subnetConfig.Name))
                        subnetConfig.Name = "default";


                    subnetConfig.IpPools ??= Array.Empty<IpPoolConfig>();

                    foreach (var ipPoolConfig in subnetConfig.IpPools)
                    {
                        if(string.IsNullOrEmpty(ipPoolConfig.Name))
                            ipPoolConfig.Name = "default";

                        if (!string.IsNullOrWhiteSpace(ipPoolConfig.FirstIp)
                            && IPAddress.TryParse(ipPoolConfig.FirstIp, out var firstIp))
                        {
                            ipPoolConfig.FirstIp = firstIp.ToString();
                        }

                        if (!string.IsNullOrWhiteSpace(ipPoolConfig.LastIp)
                            && IPAddress.TryParse(ipPoolConfig.LastIp, out var lastIp))
                        {
                            ipPoolConfig.LastIp = lastIp.ToString();
                        }
                    }
                }
            }


            return config;
        }

        private static async Task<T[]> AsyncToArray<T>(IAsyncEnumerable<T> items)
        {
            var results = new List<T>();
            await foreach (var item in items
                               .ConfigureAwait(false))
                results.Add(item);
            return results.ToArray();
        }

        private async IAsyncEnumerable<string> ValidateChanges(Guid projectId,
            NetworkProvidersConfiguration providerConfig)
        {
            var savedNetworks = await _stateStore
                .For<VirtualNetwork>()
                .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(projectId));

            // network validation (deleted)
            foreach (var network in savedNetworks)
            {
                if(Data.Config!.Networks.Any(x => x.Name == network.Name))
                    continue;

                var countOfCatletPorts = network.NetworkPorts.Count(x => x is CatletNetworkPort);

                if(countOfCatletPorts == 0)
                    continue;

                _log.LogDebug("network '{networkName}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.", network.Name, countOfCatletPorts);


                yield return
                    $"network '{network.Name}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.";

            }


            // network validation (change)
            foreach (var networkConfig in Data.Config!.Networks)
            {
                var network = savedNetworks.Find(x => x.Name == networkConfig.Name);
                if(network == null)
                    continue;

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = providerConfig.NetworkProviders.First(x => x.Name == providerName);
                if(networkProvider.Type == NetworkProviderType.Flat) continue;

                var countOfCatletPorts = network.NetworkPorts.Count(x => x is CatletNetworkPort);

                // used ports validation 
                if (countOfCatletPorts > 0)
                {
                    _log.LogDebug(
                        "network '{networkName}': Network is in use ({countOfCatletPorts} ports) - checking for prohibited changes.",
                        network.Name, countOfCatletPorts);

                    var anyError = false;
                    var messagePrefix =
                        $"network '{networkConfig.Name}': Network is in use ({countOfCatletPorts} ports) - ";
                    var providerPorts = network.NetworkPorts
                        .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

                    var hasProviderPort = providerPorts.Any(x => x.ProviderName == providerName &&
                                                                 x.SubnetName == providerSubnet &&
                                                                 x.PoolName == providerIpPool);
                    if (!hasProviderPort)
                    {
                        _log.LogDebug("network '{networkName}': Detected unsupported change of provider port.",
                            network.Name);

                        yield return $"{messagePrefix} changing network provider is not supported.'";
                        anyError = true;
                    }

                    var currentSubnets = network.Subnets.Select(x => x.IpNetwork).Distinct();
                    var newSubnets = networkConfig.Subnets
                        .Select(x => x.Address ?? networkConfig.Address).Distinct();

                    if (!currentSubnets.ToSeq().SequenceEqual(newSubnets))
                    {
                        _log.LogDebug(
                            "network '{networkName}': Detected unsupported change of network address. current subnets: {@currentSubnets}, new subnets: {@newSubnets}",
                            network.Name, currentSubnets, newSubnets);

                        yield return $"{messagePrefix} changing addresses is not supported'";
                        anyError = true;
                    }

                    if (anyError)
                        yield return
                            $"network '{networkConfig.Name}': To change the network first remove all ports or move them to another network.";
                }

                // ip pool validation (deleted pools)
                foreach (var subnet in network.Subnets)
                {

                    foreach (var ipPool in subnet.IpPools)    
                    {
                        await _stateStore.LoadCollectionAsync(ipPool, x => x.IpAssignments);
                        if (ipPool.IpAssignments.Count == 0)
                            continue;

                        var subnetConfig = networkConfig.Subnets.FirstOrDefault(x => x.Name == subnet.Name);
                        var poolConfig = subnetConfig?.IpPools.FirstOrDefault(x => x.Name == ipPool.Name);

                        if(poolConfig == null)
                            yield return
                                $"ip pool '{networkConfig.Name}/{subnet.Name}/{ipPool.Name}': Cannot delete a used ip pool ({ipPool.IpAssignments.Count} ip assignments found) .";

                    }
                }

                // ip pool validation (changed pools)
                foreach (var subnetConfig in networkConfig.Subnets)
                {
                    var subnet = network.Subnets.FirstOrDefault(x => x.Name == subnetConfig.Name);
                    if (subnet == null) continue;

                    foreach (var ipPoolConfig in subnetConfig.IpPools)
                    {
                        _log.LogTrace("Validating ip pool config: {@config}", ipPoolConfig);
                        var ipPool = subnet.IpPools.FirstOrDefault(x => x.Name == ipPoolConfig.Name);

                        if (ipPool == null) continue;

                        await _stateStore.LoadCollectionAsync(ipPool, x => x.IpAssignments);

                        if (ipPool.IpAssignments.Count == 0) continue;

                        if (ipPool.FirstIp != ipPoolConfig.FirstIp)
                            yield return
                                $"ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Changing the first ip of a used ip pool is not supported.";

                        var effectiveNetwork = subnetConfig.Address ?? networkConfig.Address;

                        if (ipPool.IpNetwork != effectiveNetwork)
                            yield return
                                $"ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Changing the address of a used ip pool is not supported.";

                        //check if it possible to move last ip
                        if (ipPoolConfig.LastIp == ipPool.LastIp) continue;

                        var maxIp = ipPool.IpAssignments
                            .Select(x => IPNetwork.ToBigInteger(IPAddress.Parse(x.IpAddress)))
                            .Max();

                        var lastIpNo = IPNetwork.ToBigInteger(IPAddress.Parse(ipPoolConfig.LastIp));
                        var maxAsIp = IPNetwork.ToIPAddress(maxIp, AddressFamily.InterNetwork);

                        if (maxIp > lastIpNo)
                            yield return
                                $"ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Cannot change last ip to '{ipPoolConfig.LastIp}' as there are already higher addresses assigned (e.g.: '{maxAsIp}').";
                    }
                }
            }
        }

        private IEnumerable<string> ValidateConfig(NetworkProvidersConfiguration providerConfig)
        {
            var ipNetworksOfNetworks =
                Data.Config!.Networks.Select(x =>
                        (IPNetwork.TryParse(x.Address, out var ipNetwork), ipNetwork, x.Name))
                    .Where(x => x.Item1)
                    .Select(x => (Address: x.ipNetwork, Network: x.Name))
                    .ToArray();


            foreach (var networkConfig in Data.Config!.Networks)
            {
                if (string.IsNullOrWhiteSpace(networkConfig.Name))
                    yield return "Empty network name";

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnetName = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPoolName = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = providerConfig.NetworkProviders.FirstOrDefault(x => x.Name == providerName);

                if (networkProvider == null)
                {
                    yield return $"network '{networkConfig.Name}': could not find network provider '{providerName}'";
                    continue;
                }

                if (networkProvider.Type == NetworkProviderType.Flat)
                {
                    if (providerIpPoolName != "default" || providerSubnetName != "default")
                    {
                        yield return $"network '{networkConfig.Name}': provider subnets and ip pools are not supported for flat networks.";
                    }

                    if (networkConfig.Subnets.Length > 0)
                        yield return $"network '{networkConfig.Name}': subnet config not supported for flat networks.";

                }
                else
                {
                    var providerSubnet = networkProvider.Subnets
                        .FirstOrDefault(x => x.Name == providerSubnetName);

                    if (providerSubnet == null)
                    {
                        yield return $"network '{networkConfig.Name}': provider subnet '{providerName}/{providerSubnetName}' not found";
                        continue;
                    }

                    if (providerSubnet.IpPools.All(x => x.Name != providerIpPoolName))
                    {
                        yield return $"network '{networkConfig.Name}': provider ip pool '{providerName}/{providerSubnetName}/{providerIpPoolName}' not found";
                        continue;
                    }

                    if (!IPNetwork.TryParse(networkConfig.Address, out var networkIpNetwork))
                    {
                        yield return
                            $"network '{networkConfig.Name}': Invalid network address '{networkConfig.Address}'";
                        continue;
                    }

                    if (networkIpNetwork.AddressFamily != AddressFamily.InterNetwork)
                        yield return
                            $"network '{networkConfig.Name}': network address '{networkConfig.Address}' is not a IPV4 address'";

                    if (networkIpNetwork.ToString() != networkConfig.Address)
                    {
                        yield return
                            $"network '{networkConfig.Name}': Invalid network address '{networkConfig.Address}' - network cidr match network '{networkIpNetwork}'";
                        continue;

                    }

                    var overlappingNetworks = string.Join(',', ipNetworksOfNetworks
                        .Where(x => x.Network != networkConfig.Name
                                    && networkIpNetwork.Overlap(x.Address))
                        .Select(x => x.Network).Distinct());

                    if (!string.IsNullOrWhiteSpace(overlappingNetworks))
                    {
                        yield return
                            $"network '{networkConfig.Name}': network address '{networkConfig.Address}' overlap with network(s) '{overlappingNetworks}'";
                        continue;

                    }


                    foreach (var subnetConfig in networkConfig.Subnets)
                    {
                        var subnetIPNetwork = networkIpNetwork;
                        var subnetAddress = subnetConfig.Address ?? networkConfig.Address;
                        if (!string.IsNullOrWhiteSpace(subnetConfig.Address))
                        {
                            if (!IPNetwork.TryParse(subnetConfig.Address, out subnetIPNetwork))
                                yield return
                                    $"subnet '{networkConfig.Name}/{subnetConfig.Name}': Invalid network address '{subnetConfig.Address}'";

                            if (!networkIpNetwork.Contains(subnetIPNetwork))
                                yield return
                                    $"subnet '{networkConfig.Name}/{subnetConfig.Name}': network address '{subnetIPNetwork}' is not a subnet of '{networkConfig.Address}'";

                        }

                        if (subnetIPNetwork == null)
                            continue;

                        if (subnetIPNetwork.ToString() != subnetAddress)
                        {
                            yield return
                                $"subnet '{networkConfig.Name}/{subnetConfig.Name}': Invalid network address '{subnetAddress}' - network cidr match network '{subnetIPNetwork}'";

                        }

                        foreach (var poolConfig in subnetConfig.IpPools)
                        {
                            if (!IPAddress.TryParse(poolConfig.FirstIp, out var firstIp))
                                yield return
                                    $"ip pool '{subnetConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Invalid ip address '{poolConfig.FirstIp}'";
                            if (!IPAddress.TryParse(poolConfig.LastIp, out var lastIp))
                                yield return
                                    $"ip pool '{subnetConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Invalid ip address '{poolConfig.LastIp}'";

                            if (firstIp != null && !subnetIPNetwork.Contains(firstIp))
                                yield return
                                    $"ip pool '{subnetConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': ip address '{poolConfig.FirstIp}' is not in subnet '{subnetIPNetwork}'";

                            if (lastIp != null && !subnetIPNetwork.Contains(lastIp))
                                yield return
                                    $"ip pool '{subnetConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': ip address '{poolConfig.LastIp}' is not in subnet '{subnetIPNetwork}'";

                            if (lastIp != null && firstIp != null &&
                                IPNetwork.ToBigInteger(lastIp) < IPNetwork.ToBigInteger(firstIp))
                                yield return
                                    $"ip pool '{subnetConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}':last ip address '{poolConfig.LastIp}' is not larger then first ip address '{poolConfig.FirstIp}'";

                        }
                    }
                }
            }

        }

        private async Task UpdateNetwork(Guid projectId, NetworkProvidersConfiguration providerConfig)
        {
            var savedNetworks = await _stateStore
                .For<VirtualNetwork>()
                .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(projectId));

            var foundNames = new List<string>();
            foreach (var networkConfig in Data.Config.Networks)
            {
                var savedNetwork = savedNetworks.Find(x => x.Name == networkConfig.Name);
                if (savedNetwork == null)
                {
                    _log.LogDebug("network {network} not found. Creating new network.", networkConfig.Name);
                    var newNetwork = new VirtualNetwork
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Name = networkConfig.Name,
                        Subnets = new List<VirtualNetworkSubnet>(),
                        NetworkPorts = new List<VirtualNetworkPort>(),
                    };

                    savedNetworks.Add(newNetwork);
                    await _stateStore.For<VirtualNetwork>().AddAsync(newNetwork);
                }

                foundNames.Add(networkConfig.Name);

            }

            var removeNetworks = savedNetworks.Where(x => !foundNames.Contains(x.Name)).ToArray();
            if (removeNetworks.Any()) 
                _log.LogDebug("Removing networks: {@removedNetworks}", (object) removeNetworks);

            await _stateStore.For<VirtualNetwork>().DeleteRangeAsync(removeNetworks);

            // second pass - update of new or existing records
            foreach (var networkConfig in Data.Config.Networks.DistinctBy(x => x.Name))
            {
                var savedNetwork = savedNetworks.First(x => x.Name == networkConfig.Name);

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

                _log.LogDebug("Updating network {network}", savedNetwork.Name);

                var networkProvider = providerConfig.NetworkProviders.First(x => x.Name == providerName);
                var isFlatNetwork = networkProvider.Type == NetworkProviderType.Flat;

                savedNetwork.NetworkProvider = providerName;
                savedNetwork.IpNetwork = networkConfig.Address;

                if (isFlatNetwork)
                {
                    //remove all existing overlay network objects if provider is a flat network
                    savedNetwork.RouterPort = null;
                    savedNetwork.Subnets.Clear();
                    foreach (var port in savedNetwork.NetworkPorts.ToSeq())
                    {
                        if (port is NetworkRouterPort or ProviderRouterPort)
                            savedNetwork.NetworkPorts.Remove(port);

                        await _stateStore.LoadCollectionAsync(port, x => x.IpAssignments);
                        port.IpAssignments.Clear();
                    }

                    continue;
                }

                var networkAddress = IPNetwork.Parse(networkConfig.Address);

                var providerPorts = savedNetwork.NetworkPorts
                    .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

                //remove all ports if more then one
                if (providerPorts.Length > 1)
                {
                    _log.LogWarning("Found invalid provider port count ({count} provider ports) for {network}. Removing all provider ports.", providerPorts.Length, savedNetwork.Name);

                    await _stateStore.For<ProviderRouterPort>().DeleteRangeAsync(providerPorts);
                    providerPorts = Array.Empty<ProviderRouterPort>();
                }

                var providerPort = providerPorts.FirstOrDefault();
                if (providerPort != null)
                {
                    if (providerPort.ProviderName != providerName ||
                        providerPort.SubnetName != providerSubnet ||
                        providerPort.PoolName != providerIpPool)
                    {
                        _log.LogInformation("Network {network}: network provider settings changed.", savedNetwork.Name);

                        savedNetwork.NetworkPorts.Remove(providerPort);
                        providerPort = null;
                    }
                }

                if (providerPort == null)
                {
                    savedNetwork.NetworkPorts.Add(new ProviderRouterPort()
                    {
                        Name = "provider",
                        SubnetName = providerSubnet,
                        PoolName = providerIpPool,
                        MacAddress = MacAddresses.FormatMacAddress(
                            MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                        ProviderName = providerName,
                    });
                }

                var routerPorts = savedNetwork.NetworkPorts
                    .Where(x => x is NetworkRouterPort).Cast<NetworkRouterPort>().ToArray();

                //remove all ports if more then one
                if (routerPorts.Length > 1)
                {
                    _log.LogWarning("Found invalid router port count ({count} router ports) for {network}. Removing all router ports.", routerPorts.Length, savedNetwork.Name);

                    await _stateStore.For<NetworkRouterPort>().DeleteRangeAsync(routerPorts);
                    routerPorts = Array.Empty<NetworkRouterPort>();
                }

                var routerPort = routerPorts.FirstOrDefault();


                if (routerPort != null && routerPort.Id != savedNetwork.RouterPort.Id)
                {
                    savedNetwork.NetworkPorts.Remove(routerPort);
                    routerPort = null;
                }

                if (routerPort != null)
                {
                    await _stateStore.LoadCollectionAsync(routerPort, x => x.IpAssignments);
                    var ipAssignment = routerPort.IpAssignments.FirstOrDefault();

                    if (ipAssignment == null ||
                        !IPAddress.TryParse(ipAssignment.IpAddress, out var ipAddress) ||
                        !networkAddress.Contains(ipAddress))
                    {
                        _log.LogInformation("Network {network}: network router ip assignment changed to {ipAddress}.", savedNetwork.Name, networkAddress.FirstUsable);

                        savedNetwork.NetworkPorts.Remove(routerPort);
                        routerPort = null;
                    }
                }

                if (routerPort == null)
                {
                    routerPort = new NetworkRouterPort
                    {
                        MacAddress = MacAddresses.FormatMacAddress(
                            MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                        IpAssignments = new List<IpAssignment>(new[]
                        {
                            new IpAssignment
                            {
                                IpAddress = networkAddress.FirstUsable.ToString(),
                            }
                        }),
                        Name = "default",
                        RoutedNetworkId = savedNetwork.Id,
                        NetworkId = savedNetwork.Id,
                    };

                    savedNetwork.RouterPort = routerPort;
                    savedNetwork.NetworkPorts.Add(routerPort);
                }


                foundNames.Clear();

                foreach (var subnetConfig in networkConfig.Subnets)
                {
                    foundNames.Add(subnetConfig.Name);

                    var savedSubnet = savedNetwork.Subnets.FirstOrDefault(x => x.Name ==
                                                                               subnetConfig.Name);
                    if (savedSubnet == null)
                    {
                        _log.LogDebug("subnet {network}/{subnet} not found. Creating new subnet.", networkConfig.Name, subnetConfig.Name);

                        savedNetwork.Subnets.Add(new VirtualNetworkSubnet
                        {
                            Name = subnetConfig.Name,
                            IpPools = new List<IpPool>(),
                            NetworkId = savedNetwork.Id
                        });
                    }
                }

                var removeSubnets = savedNetwork.Subnets
                    .Where(x => !foundNames.Contains(x.Name)).ToArray();

                if (removeSubnets.Any())
                    _log.LogDebug("Removing subnets: {@removeSubnets}", (object) removeSubnets);

                await _stateStore.For<VirtualNetworkSubnet>().DeleteRangeAsync(removeSubnets);

                foreach (var subnetConfig in networkConfig.Subnets.DistinctBy(x => x.Name))
                {
                    var savedSubnet = savedNetwork.Subnets.First(x => x.Name == subnetConfig.Name);

                    _log.LogDebug("Updating subnet {network}/{subnet}", savedNetwork.Name, savedSubnet.Name);


                    savedSubnet.DhcpLeaseTime = 3600;
                    savedSubnet.MTU = subnetConfig.Mtu;
                    savedSubnet.DnsServersV4 = subnetConfig.DnsServers != null
                        ? string.Join(',', subnetConfig.DnsServers)
                        : null;
                    savedSubnet.IpNetwork = subnetConfig.Address ?? networkConfig.Address;


                    foundNames.Clear();

                    foreach (var ipPoolConfig in subnetConfig.IpPools.DistinctBy(x => x.Name))
                    {
                        foundNames.Add(ipPoolConfig.Name);

                        var savedIpPool = savedSubnet.IpPools.FirstOrDefault(x => x.Name == ipPoolConfig.Name);

                        // ip pool recreation - validation has ensured that it is no longer in use
                        if (savedIpPool != null && savedIpPool.IpNetwork != savedSubnet.IpNetwork)
                        {
                            savedSubnet.IpPools.Remove(savedIpPool);
                            savedIpPool = null;
                        }

                        // change of ip pool is allowed if validation has passed (enough space in pool or unused)
                        if (savedIpPool != null)
                        {
                            _log.LogDebug("Updating ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
                                savedSubnet.Name, savedIpPool.Name);

                            savedIpPool.IpNetwork = savedSubnet.IpNetwork;
                            savedIpPool.FirstIp = ipPoolConfig.FirstIp;
                            savedIpPool.LastIp = ipPoolConfig.LastIp;
                        }

                        if (savedIpPool == null)
                        {
                            _log.LogDebug("creating new ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
                                savedSubnet.Name, ipPoolConfig.Name);

                            savedSubnet.IpPools.Add(new IpPool
                            {
                                Name = ipPoolConfig.Name,
                                FirstIp = ipPoolConfig.FirstIp,
                                LastIp = ipPoolConfig.LastIp,
                                IpNetwork = savedSubnet.IpNetwork
                            });
                        }
                    }

                    var removeIpPools = savedSubnet.IpPools.Where(x => !foundNames.Contains(x.Name));
                    await _stateStore.For<IpPool>().DeleteRangeAsync(removeIpPools);
                }
            }

        }

        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }
    }
}