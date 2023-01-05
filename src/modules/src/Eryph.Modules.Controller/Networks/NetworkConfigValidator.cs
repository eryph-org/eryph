using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Networks
{
    public class NetworkConfigValidator : INetworkConfigValidator
    {
        private readonly IStateStore _stateStore;
        private readonly ILogger _log;

        public NetworkConfigValidator(IStateStore stateStore, ILogger log)
        {
            _stateStore = stateStore;
            _log = log;
        }

        public ProjectNetworksConfig NormalizeConfig(ProjectNetworksConfig config)
        {

            foreach (var networkConfig in config.Networks)
            {

                networkConfig.Subnets ??= Array.Empty<NetworkSubnetConfig>();

                foreach (var subnetConfig in networkConfig.Subnets)
                {
                    if (string.IsNullOrWhiteSpace(subnetConfig.Name))
                        subnetConfig.Name = "default";


                    subnetConfig.IpPools ??= Array.Empty<IpPoolConfig>();

                    foreach (var ipPoolConfig in subnetConfig.IpPools)
                    {
                        if (string.IsNullOrEmpty(ipPoolConfig.Name))
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

        public async IAsyncEnumerable<string> ValidateChanges(Guid projectId,
            ProjectNetworksConfig config,
            NetworkProvider[] networkProviders)
        {
            var savedNetworks = await _stateStore
                .For<VirtualNetwork>()
                .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(projectId));

            // network validation (deleted)
            foreach (var network in savedNetworks)
            {
                if (config.Networks.Any(x => x.Name == network.Name))
                    continue;
                var countOfCatletPorts = network.NetworkPorts.Count(x => x is CatletNetworkPort);
                if (countOfCatletPorts == 0)
                    continue;

                _log.LogDebug("network '{networkName}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.", network.Name, countOfCatletPorts);


                yield return
                    $"network '{network.Name}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.";

            }


            // network validation (change)
            foreach (var networkConfig in config.Networks)
            {
                var network = savedNetworks.Find(x => x.Name == networkConfig.Name);
                if (network == null)
                    continue;

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = networkProviders.First(x => x.Name == providerName);
                if (networkProvider.Type == NetworkProviderType.Flat) continue;

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

                        if (poolConfig == null)
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

        public IEnumerable<string> ValidateConfig(ProjectNetworksConfig config, NetworkProvider[] networkProviders)
        {
            var ipNetworksOfNetworks =
                config.Networks.Select(x =>
                        (IPNetwork.TryParse(x.Address, out var ipNetwork), ipNetwork, x.Name))
                    .Where(x => x.Item1)
                    .Select(x => (Address: x.ipNetwork, Network: x.Name))
                    .ToArray();


            foreach (var networkConfig in config.Networks)
            {
                if (string.IsNullOrWhiteSpace(networkConfig.Name))
                    yield return "Empty network name";

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnetName = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPoolName = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = networkProviders.FirstOrDefault(x => x.Name == providerName);

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

                    //do not check - subnets will be removed silently
                    //if (networkConfig.Subnets.Length > 0)
                    //    yield return $"network '{networkConfig.Name}': subnet config not supported for flat networks.";

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

    }
}
