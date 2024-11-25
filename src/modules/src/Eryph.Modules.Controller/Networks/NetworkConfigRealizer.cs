using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Networks;

public class NetworkConfigRealizer(
    IStateStore stateStore,
    IIpPoolManager ipPoolManager,
    ILogger log)
    : INetworkConfigRealizer
{
    private static string GetEnvironmentName(string? environment, string network)
    {
        return environment == null 
            ? $"env:default-{network}" 
            : $"env:{environment}-{network}";
    }

    public async Task UpdateNetwork(Guid projectId, ProjectNetworksConfig config, NetworkProvidersConfiguration providerConfig)
    {
        var savedNetworks = await stateStore
            .For<VirtualNetwork>()
            .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(projectId));

        var foundNames = new List<string>();
        var networkConfigs = config.Networks?.Where(x=>x.Name!= null).ToArray() ?? Array.Empty<NetworkConfig>();
        foreach (var networkConfig in networkConfigs)
        {
            if (networkConfig.Name == null)
                continue;

            var networkEnvName = GetEnvironmentName(networkConfig.Environment, networkConfig.Name);
            var savedNetwork = savedNetworks.Find(x => GetEnvironmentName(x.Environment, x.Name) == networkEnvName);
            if (savedNetwork == null)
            {
                log.LogDebug("Environment {env}: network {network} not found. Creating new network.",
                    networkConfig.Environment ?? EryphConstants.DefaultEnvironmentName,
                    networkConfig.Name);
                var newNetwork = new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Environment = networkConfig.Environment ?? EryphConstants.DefaultEnvironmentName,
                    Name = networkConfig.Name,
                    NetworkProvider = networkConfig.Provider?.Name ?? EryphConstants.DefaultProviderName,
                    Subnets = new List<VirtualNetworkSubnet>(),
                    NetworkPorts = new List<VirtualNetworkPort>(),
                };

                savedNetworks.Add(newNetwork);
                await stateStore.For<VirtualNetwork>().AddAsync(newNetwork);
            }

            foundNames.Add(networkEnvName);

        }

        var removeNetworks = savedNetworks.Where(x => !foundNames.Contains(GetEnvironmentName(x.Environment,x.Name))).ToArray();
        if (removeNetworks.Any())
            log.LogDebug("Removing networks: {@removedNetworks}", (object)removeNetworks);

        await stateStore.For<VirtualNetwork>().DeleteRangeAsync(removeNetworks);

        // second pass - update of new or existing records
        foreach (var networkConfig in networkConfigs.DistinctBy(x => GetEnvironmentName(x.Environment,x.Name!)))
        {
            if (networkConfig.Name == null) // can't happen
                continue;

            var networkEnvName = GetEnvironmentName(networkConfig.Environment, networkConfig.Name);
            var savedNetwork = savedNetworks.First(x => GetEnvironmentName(x.Environment, x.Name) == networkEnvName);

            var providerName = networkConfig.Provider?.Name ?? "default";
            var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
            var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

            log.LogDebug("Environment {env}: Updating network {network}", savedNetwork.Environment ?? "default", savedNetwork.Name);

            var networkProvider = providerConfig.NetworkProviders.FirstOrDefault(x => x.Name == providerName) 
                                  ?? throw new InconsistentNetworkConfigException($"Network provider {providerName} not found.");
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

                    await stateStore.LoadCollectionAsync(port, x => x.IpAssignments);
                    port.IpAssignments.Clear();
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(networkConfig.Address))
                throw new InconsistentNetworkConfigException($"Network '{networkConfig.Name}': Network address not set.");

            var networkAddress = IPNetwork2.Parse(networkConfig.Address);

            var providerPorts = savedNetwork.NetworkPorts
                .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

            //remove all ports if more than one
            if (providerPorts.Length > 1)
            {
                log.LogWarning("Found invalid provider port count ({count} provider ports) for {network}. Removing all provider ports.", providerPorts.Length, savedNetwork.Name);

                await stateStore.For<ProviderRouterPort>().DeleteRangeAsync(providerPorts);
                providerPorts = Array.Empty<ProviderRouterPort>();
            }

            var providerPort = providerPorts.FirstOrDefault();
            if (providerPort != null)
            {
                if (providerPort.ProviderName != providerName ||
                    providerPort.SubnetName != providerSubnet ||
                    providerPort.PoolName != providerIpPool)
                {
                    log.LogInformation("Network {network}: network provider settings changed.", savedNetwork.Name);

                    savedNetwork.NetworkPorts.Remove(providerPort);
                    providerPort = null;
                }
            }

            if (providerPort == null)
            {
                providerPort = new ProviderRouterPort()
                {
                    Name = "provider",
                    SubnetName = providerSubnet,
                    PoolName = providerIpPool,
                    MacAddress = MacAddresses.FormatMacAddress(
                        MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                    ProviderName = providerName,
                    IpAssignments = [],
                };

                savedNetwork.NetworkPorts.Add(providerPort);
            }

            if (providerPort.IpAssignments.Count == 0)
            {
                var ipAssignment = await AcquireProviderIp(providerName, providerSubnet, providerIpPool);
                providerPort.IpAssignments.Add(ipAssignment);
            }

            var routerPorts = savedNetwork.NetworkPorts
                .Where(x => x is NetworkRouterPort).Cast<NetworkRouterPort>().ToArray();

            //remove all ports if more then one
            if (routerPorts.Length > 1)
            {
                log.LogWarning("Found invalid router port count ({count} router ports) for {network}. Removing all router ports.", routerPorts.Length, savedNetwork.Name);

                await stateStore.For<NetworkRouterPort>().DeleteRangeAsync(routerPorts);
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
                await stateStore.LoadCollectionAsync(routerPort, x => x.IpAssignments);
                var ipAssignment = routerPort.IpAssignments.FirstOrDefault();

                if (ipAssignment == null ||
                    !IPAddress.TryParse(ipAssignment.IpAddress, out var ipAddress) ||
                    !networkAddress.Contains(ipAddress))
                {
                    log.LogInformation("Environment {env}, Network {network}: network router ip assignment changed to {ipAddress}.",
                        savedNetwork.Environment ?? "default", savedNetwork.Name, networkAddress.FirstUsable);

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

            // if nothing has been configured for ip pool, we need second address after router to initialize pool
            var secondIp = IPNetwork2.ToIPAddress(IPNetwork2.ToBigInteger(networkAddress.FirstUsable) + 1, AddressFamily.InterNetwork);

            networkConfig.Subnets ??= new[]
            {
                new NetworkSubnetConfig
                {
                    Name = "default",
                    Address = networkConfig.Address,

                    // default project restore ? in that case also use default dns servers
                    DnsServers = projectId == EryphConstants.DefaultProjectId ? new [] {"9.9.9.9","8.8.8.8"} : null,
                    Mtu = 1400,
                    IpPools = new IpPoolConfig[]
                    {
                        new(){
                            Name = "default",
                            // default projects network network? if not try to initialize pool even if not completely configured
                            FirstIp = networkConfig.Address == "10.0.0.0/20" ? "10.0.0.100" :  secondIp.ToString(),
                            LastIp =  networkConfig.Address == "10.0.0.0/20" ? "10.0.2.240" : IPNetwork2.Parse(networkConfig.Address).LastUsable.ToString(),
                        },
                    }
                }
            };

            foreach (var subnetConfig in networkConfig.Subnets)
            {
                if(subnetConfig.Name == null)
                    continue;

                foundNames.Add(subnetConfig.Name);

                var savedSubnet = savedNetwork.Subnets.FirstOrDefault(x => x.Name ==
                                                                           subnetConfig.Name);
                if (savedSubnet == null)
                {
                    log.LogDebug("subnet {network}/{subnet} not found. Creating new subnet.", networkConfig.Name, subnetConfig.Name);

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
                log.LogDebug("Removing subnets: {@removeSubnets}", (object)removeSubnets);

            await stateStore.For<VirtualNetworkSubnet>().DeleteRangeAsync(removeSubnets);

            foreach (var subnetConfig in networkConfig.Subnets.DistinctBy(x => x.Name).ToArray())
            {
                var savedSubnet = savedNetwork.Subnets.FirstOrDefault(x => x.Name == subnetConfig.Name)
                                  ?? throw new InconsistentNetworkConfigException($"Subnet {subnetConfig} not found in network {savedNetwork.Name}");

                log.LogDebug("Updating subnet {network}/{subnet}", savedNetwork.Name, savedSubnet.Name);

                savedSubnet.DhcpLeaseTime = 3600;
                savedSubnet.MTU = subnetConfig.Mtu.GetValueOrDefault() == 0 ? 1400 : subnetConfig.Mtu.GetValueOrDefault();
                savedSubnet.DnsServersV4 = subnetConfig.DnsServers != null
                    ? string.Join(',', subnetConfig.DnsServers)
                    : null;
                savedSubnet.IpNetwork = subnetConfig.Address ?? networkConfig.Address;

                // default domain is home.arpa
                // except for environments where the default is [environment].home.arpa
                // user set domain names are always preferred (even if this could result in duplicate names)
                savedSubnet.DnsDomain = savedNetwork.Environment is null or "default" 
                            ? subnetConfig.DnsDomain ?? "home.arpa"
                            : subnetConfig.DnsDomain ?? $"{(savedNetwork.Environment??"").ToLowerInvariant()}.home.arpa";
                subnetConfig.IpPools ??= Array.Empty<IpPoolConfig>();

                foundNames.Clear();

                foreach (var ipPoolConfig in subnetConfig.IpPools.DistinctBy(x => x.Name))
                {
                    if(ipPoolConfig.Name == null)
                        continue;

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
                        log.LogDebug("Updating ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
                            savedSubnet.Name, savedIpPool.Name);

                        savedIpPool.IpNetwork = savedSubnet.IpNetwork;
                        savedIpPool.FirstIp = ipPoolConfig.FirstIp;
                        // Use the current next IP when the next IP has not been specified explicitly.
                        // We want to avoid unnecessary changes to the next IP as it represents the
                        // current state of the IP pool and is used to prevent reissuing of IP addresses
                        // unless absolutely necessary.
                        savedIpPool.NextIp = ipPoolConfig.NextIp ?? savedIpPool.NextIp;
                        savedIpPool.LastIp = ipPoolConfig.LastIp;
                    }

                    if (savedIpPool == null)
                    {
                        log.LogDebug("creating new ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
                            savedSubnet.Name, ipPoolConfig.Name);

                        savedSubnet.IpPools.Add(new IpPool
                        {
                            Name = ipPoolConfig.Name,
                            FirstIp = ipPoolConfig.FirstIp,
                            NextIp = ipPoolConfig.NextIp ?? ipPoolConfig.FirstIp,
                            LastIp = ipPoolConfig.LastIp,
                            IpNetwork = savedSubnet.IpNetwork
                        });
                    }
                }

                var removeIpPools = savedSubnet.IpPools.Where(x => !foundNames.Contains(x.Name));
                await stateStore.For<IpPool>().DeleteRangeAsync(removeIpPools);
            }
        }
    }

    private async Task<IpPoolAssignment> AcquireProviderIp(
        string providerName,
        string subnetName,
        string poolName)
    {
        var providerSubnet = await stateStore.Read<ProviderSubnet>().GetBySpecAsync(
            new ProviderSubnetSpecs.GetByName(providerName, subnetName));
        if (providerSubnet is null)
        {
            throw new InconsistentNetworkConfigException(
                $"Subnet {subnetName} of provider {providerName} not found.");
        }
        
        var result = await ipPoolManager.AcquireIp(providerSubnet.Id, poolName);
        if (result.IsLeft)
        {
            throw new InconsistentNetworkConfigException(
                $"Failed to configure IP for provider {providerName}: {result.LeftToSeq().Head.Message}");
        }

        return result.ValueUnsafe();
    }
}
