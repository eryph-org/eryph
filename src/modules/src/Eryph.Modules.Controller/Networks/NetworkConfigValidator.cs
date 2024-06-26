﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Eryph.ConfigModel.Networks;
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

        private static string GetEnvironmentName(string? environment, string network)
        {
            return environment == null
                ? $"env:default-{network}"
                : $"env:{environment}-{network}";
        }

        public ProjectNetworksConfig NormalizeConfig(ProjectNetworksConfig config)
        {

            foreach (var networkConfig in config.Networks ?? Array.Empty<NetworkConfig>())
            {
                if(string.IsNullOrWhiteSpace(networkConfig.Environment))
                    networkConfig.Environment = "default";

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

                        if (!string.IsNullOrWhiteSpace(ipPoolConfig.NextIp)
                            && IPAddress.TryParse(ipPoolConfig.NextIp, out var nextIp))
                        {
                            ipPoolConfig.NextIp = nextIp.ToString();
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

            var networkConfigs = config.Networks ?? Array.Empty<NetworkConfig>();
            // network validation (deleted)
            foreach (var network in savedNetworks)
            {
                var envName = GetEnvironmentName(network.Environment, network.Name);
                if (networkConfigs.Any(x => GetEnvironmentName(x.Environment, x.Name ?? "default") == envName))
                    continue;

                var countOfCatletPorts = network.NetworkPorts.Count(x => x is CatletNetworkPort);
                if (countOfCatletPorts == 0)
                    continue;

                _log.LogDebug("environment '{env}', network '{networkName}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.", 
                    network.Environment,
                    network.Name, countOfCatletPorts);


                yield return
                    $"environment '{network.Environment}', network '{network.Name}': Network is in use ({countOfCatletPorts} ports) - cannot remove network.";

            }


            // network validation (change)
            foreach (var networkConfig in networkConfigs)
            {
                var network = savedNetworks.Find(x => x.Environment == networkConfig.Environment && x.Name == networkConfig.Name);
                if (network == null)
                    continue;

                var subnetConfigs = networkConfig.Subnets ?? Array.Empty<NetworkSubnetConfig>();
                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = networkProviders.FirstOrDefault(x => x.Name == providerName);

                if (networkProvider == null)
                {
                    yield return $"environment '{networkConfig.Environment}', network '{networkConfig.Name}': could not find network provider '{providerName}'";
                    continue;
                }

                if (networkProvider.Type == NetworkProviderType.Flat) continue;

                var countOfCatletPorts = network.NetworkPorts.Count(x => x is CatletNetworkPort);
                var environmentMessage = networkConfig.Environment == "default" ? "" : $"environment '{networkConfig.Environment}', ";

                // used ports validation 
                if (countOfCatletPorts > 0)
                {
                    _log.LogDebug(
                        "environment '{env}', network '{networkName}': Network is in use ({countOfCatletPorts} ports) - checking for prohibited changes.",
                        network.Environment,network.Name, countOfCatletPorts);

                    var anyError = false;
                    var messagePrefix =
                        $"{environmentMessage}network '{networkConfig.Name}': Network is in use ({countOfCatletPorts} ports) - ";
                    var providerPorts = network.NetworkPorts
                        .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

                    var hasProviderPort = providerPorts.Any(x => x.ProviderName == providerName &&
                                                                 x.SubnetName == providerSubnet &&
                                                                 x.PoolName == providerIpPool);
                    if (!hasProviderPort)
                    {
                        _log.LogDebug("environment {env}, network '{networkName}': Detected unsupported change of provider port.",
                             network.Environment, network.Name);

                        yield return $"{messagePrefix} changing network provider is not supported.'";
                        anyError = true;
                    }

                    var currentSubnets = network.Subnets?.Select(x => x.IpNetwork ?? "").Distinct()
                        .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();

                    var newSubnets = subnetConfigs
                        .Select(x => x.Address ?? networkConfig.Address).Distinct();

                    // could not be null here
                    if (!currentSubnets.ToSeq()!.SequenceEqual(newSubnets))
                    {
                        _log.LogDebug(
                            "network '{networkName}': Detected unsupported change of network address. current subnets: {@currentSubnets}, new subnets: {@newSubnets}",
                            network.Name, currentSubnets, newSubnets);

                        yield return $"{messagePrefix} changing addresses is not supported'";
                        anyError = true;
                    }

                    if (anyError)
                        yield return
                            $"{environmentMessage}network '{networkConfig.Name}': To change the network first remove all ports or move them to another network.";
                }

                
                // ip pool validation (deleted pools)
                foreach (var subnet in network.Subnets?.ToArray() ?? Array.Empty<VirtualNetworkSubnet>())
                {

                    foreach (var ipPool in subnet.IpPools)
                    {
                        await _stateStore.LoadCollectionAsync(ipPool, x => x.IpAssignments);
                        if (ipPool.IpAssignments.Count == 0)
                            continue;

                        var subnetConfig = subnetConfigs.FirstOrDefault(x => x.Name == subnet.Name);
                        var poolConfig = (subnetConfig?.IpPools ?? Array.Empty<IpPoolConfig>()).FirstOrDefault(x => x.Name == ipPool.Name);

                        if (poolConfig == null)
                            yield return
                                $"{environmentMessage}ip pool '{networkConfig.Name}/{subnet.Name}/{ipPool.Name}': Cannot delete a used ip pool ({ipPool.IpAssignments.Count} ip assignments found) .";

                    }
                }

                // ip pool validation (changed pools)
                foreach (var subnetConfig in subnetConfigs)
                {
                    var subnet = network.Subnets?.FirstOrDefault(x => x.Name == subnetConfig.Name);
                    if (subnet == null) continue;

                    foreach (var ipPoolConfig in subnetConfig.IpPools ?? Array.Empty<IpPoolConfig>())
                    {
                        _log.LogTrace("Validating ip pool config: {@config}", ipPoolConfig);
                        var ipPool = subnet.IpPools.FirstOrDefault(x => x.Name == ipPoolConfig.Name);

                        if (ipPool == null) continue;

                        await _stateStore.LoadCollectionAsync(ipPool, x => x.IpAssignments);

                        if (ipPool.IpAssignments.Count == 0) continue;

                        if (ipPool.FirstIp != ipPoolConfig.FirstIp)
                            yield return
                                $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Changing the first ip of a used ip pool is not supported.";

                        var effectiveNetwork = subnetConfig.Address ?? networkConfig.Address;

                        if (ipPool.IpNetwork != effectiveNetwork)
                            yield return
                                $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Changing the address of a used ip pool is not supported.";

                        //check if it possible to move last ip
                        if (ipPoolConfig.LastIp != ipPool.LastIp)
                        {
                            var maxIp = ipPool.IpAssignments
                                .Select(x => IPNetwork2.ToBigInteger(IPAddress.Parse(x.IpAddress)))
                                .Max();

                            var lastIpNo = IPNetwork2.ToBigInteger(IPAddress.Parse(ipPoolConfig.LastIp ?? maxIp.ToString()));
                            var maxAsIp = IPNetwork2.ToIPAddress(maxIp, AddressFamily.InterNetwork);

                            if (maxIp > lastIpNo)
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Cannot change last ip to '{ipPoolConfig.LastIp}' as there are already higher addresses assigned (e.g.: '{maxAsIp}').";
                        }

                        if (!string.IsNullOrWhiteSpace(ipPoolConfig.NextIp))
                        {
                            if (ipPool.IpAssignments.Any(a => a.IpAddress == ipPoolConfig.NextIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{ipPoolConfig.Name}': Cannot change next ip to '{ipPoolConfig.NextIp}' as this ip is already assigned.";
                        }
                    }
                }
            }
        }

        public IEnumerable<string> ValidateConfig(ProjectNetworksConfig config, NetworkProvider[] networkProviders)
        {
            var networkConfigs = config.Networks ?? Array.Empty<NetworkConfig>();
            var ipNetworksOfNetworks =
                networkConfigs.Select(x =>
                        (IPNetwork2.TryParse(x.Address, out var ipNetwork), ipNetwork, 
                            Name: GetEnvironmentName(x.Environment, x.Name ?? "default")))
                    .Where(x => x.Item1)
                    .Select(x => (Address: x.ipNetwork, Network: x.Name))
                    .ToArray();

            var environmentNetworkNames = 
                networkConfigs.Select(x => GetEnvironmentName(x.Environment, x.Name ?? "default")).ToList();

            foreach (var networkConfig in networkConfigs)
            {
                if (string.IsNullOrWhiteSpace(networkConfig.Name))
                    yield return "Empty network name";

                var environmentNetworkName = GetEnvironmentName(networkConfig.Environment, networkConfig.Name ?? "default");
                if (environmentNetworkNames.Count(x => x == environmentNetworkName) > 1)
                {
                    yield return
                        $"Duplicate network name '{networkConfig.Name}' in environment '{networkConfig.Environment}'";
                    environmentNetworkNames.Remove(environmentNetworkName);
                }

                var environmentMessage = networkConfig.Environment == "default" ? "": $"environment '{networkConfig.Environment}', ";
                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnetName = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPoolName = networkConfig.Provider?.IpPool ?? "default";

                var networkProvider = networkProviders.FirstOrDefault(x => x.Name == providerName);

                if (networkProvider == null)
                {
                    yield return $"{environmentMessage}network '{networkConfig.Name}': could not find network provider '{providerName}'";
                    continue;
                }

                if (networkProvider.Type == NetworkProviderType.Flat)
                {
                    if (providerIpPoolName != "default" || providerSubnetName != "default")
                    {
                        yield return $"{environmentMessage}network '{networkConfig.Name}': provider subnets and ip pools are not supported for flat networks.";
                    }

                    
                }
                else
                {
                    var providerSubnet = networkProvider.Subnets
                        .FirstOrDefault(x => x.Name == providerSubnetName);

                    if (providerSubnet == null)
                    {
                        yield return $"{environmentMessage}network '{networkConfig.Name}': provider subnet '{providerName}/{providerSubnetName}' not found";
                        continue;
                    }

                    if (providerSubnet.IpPools.All(x => x.Name != providerIpPoolName))
                    {
                        yield return $"{environmentMessage}network '{networkConfig.Name}': provider ip pool '{providerName}/{providerSubnetName}/{providerIpPoolName}' not found";
                        continue;
                    }

                    if (!IPNetwork2.TryParse(networkConfig.Address, out var networkIpNetwork))
                    {
                        yield return
                            $"{environmentMessage}network '{networkConfig.Name}': Invalid network address '{networkConfig.Address}'";
                        continue;
                    }

                    if (networkIpNetwork.AddressFamily != AddressFamily.InterNetwork)
                        yield return
                            $"{environmentMessage}network '{networkConfig.Name}': network address '{networkConfig.Address}' is not a IPV4 address'";

                    if (networkIpNetwork.ToString() != networkConfig.Address)
                    {
                        yield return
                            $"{environmentMessage}network '{networkConfig.Name}': Invalid network address '{networkConfig.Address}' - network cidr match network '{networkIpNetwork}'";
                        continue;

                    }

                    var overlappingNetworks = string.Join(',', ipNetworksOfNetworks
                        .Where(x => x.Network != GetEnvironmentName(networkConfig.Environment, networkConfig.Name ?? "default")
                                    && networkIpNetwork.Overlap(x.Address))
                        .Select(x => x.Network).Distinct());

                    if (!string.IsNullOrWhiteSpace(overlappingNetworks))
                    {
                        yield return
                            $"{environmentMessage}network '{networkConfig.Name}': network address '{networkConfig.Address}' overlap with network(s) '{overlappingNetworks}'";
                        continue;

                    }


                    foreach (var subnetConfig in networkConfig.Subnets ?? Array.Empty<NetworkSubnetConfig>())
                    {
                        var subnetIPNetwork = networkIpNetwork;
                        var subnetAddress = subnetConfig.Address ?? networkConfig.Address;
                        if (!string.IsNullOrWhiteSpace(subnetConfig.Address))
                        {
                            if (!IPNetwork2.TryParse(subnetConfig.Address, out subnetIPNetwork))
                                yield return
                                    $"{environmentMessage}subnet '{networkConfig.Name}/{subnetConfig.Name}': Invalid network address '{subnetConfig.Address}'";

                            if (subnetIPNetwork != null && !networkIpNetwork.Contains(subnetIPNetwork))
                                yield return
                                    $"{environmentMessage}subnet '{networkConfig.Name}/{subnetConfig.Name}': network address '{subnetIPNetwork}' is not a subnet of '{networkConfig.Address}'";

                        }

                        if (subnetIPNetwork == null)
                            continue;

                        if (subnetIPNetwork.ToString() != subnetAddress)
                        {
                            yield return
                                $"{environmentMessage}subnet '{networkConfig.Name}/{subnetConfig.Name}': Invalid network address '{subnetAddress}' - network cidr match network '{subnetIPNetwork}'";

                        }

                        foreach (var poolConfig in subnetConfig.IpPools ?? Array.Empty<IpPoolConfig>())
                        {
                            if (!IPAddress.TryParse(poolConfig.FirstIp, out var firstIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Invalid ip address '{poolConfig.FirstIp}'";
                            if (!IPAddress.TryParse(poolConfig.LastIp, out var lastIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Invalid ip address '{poolConfig.LastIp}'";

                            if (firstIp != null && !subnetIPNetwork.Contains(firstIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': ip address '{poolConfig.FirstIp}' is not in subnet '{subnetIPNetwork}'";

                            if (lastIp != null && !subnetIPNetwork.Contains(lastIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': ip address '{poolConfig.LastIp}' is not in subnet '{subnetIPNetwork}'";

                            if (lastIp != null && firstIp != null &&
                                IPNetwork2.ToBigInteger(lastIp) < IPNetwork2.ToBigInteger(firstIp))
                                yield return
                                    $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}':last ip address '{poolConfig.LastIp}' is not larger then first ip address '{poolConfig.FirstIp}'";

                            if (!string.IsNullOrWhiteSpace(poolConfig.NextIp))
                            {
                                if(!IPAddress.TryParse(poolConfig.NextIp, out var nextIp))
                                    yield return
                                        $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Invalid ip address '{poolConfig.NextIp}'";

                                if (nextIp != null && !subnetIPNetwork.Contains(nextIp))
                                    yield return
                                        $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': ip address '{poolConfig.NextIp}' is not in subnet '{subnetIPNetwork}'";

                                if (nextIp != null && lastIp != null && IPNetwork2.ToBigInteger(lastIp) < IPNetwork2.ToBigInteger(nextIp))
                                    yield return
                                        $"{environmentMessage}ip pool '{networkConfig.Name}/{subnetConfig.Name}/{poolConfig.Name}': Next ip address '{poolConfig.NextIp}' is invalid as it is higher than last ip address '{poolConfig.LastIp}'";
                            }
                        }
                    }
                }
            }

        }

    }
}
