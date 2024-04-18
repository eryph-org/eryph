using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Networks;

public class NetworkConfigRealizer : INetworkConfigRealizer
{
    private readonly IStateStore _stateStore;
    private readonly ILogger _log;

    public NetworkConfigRealizer(IStateStore stateStore, ILogger log)
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

    public async Task UpdateNetwork(Guid projectId, ProjectNetworksConfig config, NetworkProvidersConfiguration providerConfig)
    {
        var savedNetworks = await _stateStore
            .For<VirtualNetwork>()
            .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(projectId));

        var foundNames = new List<string>();
        var networkConfigs = config.Networks?.Where(x=>x.Name!= null).ToArray() ?? Array.Empty<NetworkConfig>();
        foreach (var networkConfig in networkConfigs)
        {
            if(networkConfig.Name == null)
                continue;

            var networkEnvName = GetEnvironmentName(networkConfig.Environment, networkConfig.Name);
            var savedNetwork = savedNetworks.Find(x => GetEnvironmentName(x.Environment, x.Name) == networkEnvName);
            if (savedNetwork == null)
            {
                _log.LogDebug("Environment {env}: network {network} not found. Creating new network.",
                    networkConfig.Environment ?? "default", networkConfig.Name);
                var newNetwork = new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Environment = networkConfig.Environment,
                    Name = networkConfig.Name,
                    Subnets = new List<VirtualNetworkSubnet>(),
                    NetworkPorts = new List<VirtualNetworkPort>(),
                };

                savedNetworks.Add(newNetwork);
                await _stateStore.For<VirtualNetwork>().AddAsync(newNetwork);
            }

            foundNames.Add(networkEnvName);

        }

        var removeNetworks = savedNetworks.Where(x => !foundNames.Contains(GetEnvironmentName(x.Environment,x.Name))).ToArray();
        if (removeNetworks.Any())
            _log.LogDebug("Removing networks: {@removedNetworks}", (object)removeNetworks);

        await _stateStore.For<VirtualNetwork>().DeleteRangeAsync(removeNetworks);

        // second pass - update of new or existing records
        foreach (var networkConfig in networkConfigs.DistinctBy(x => GetEnvironmentName(x.Environment,x.Name!)))
        {
            if(networkConfig.Name == null) // can't happen
                continue;

            var networkEnvName = GetEnvironmentName(networkConfig.Environment, networkConfig.Name);
            var savedNetwork = savedNetworks.First(x => GetEnvironmentName(x.Environment, x.Name) == networkEnvName);

            var providerName = networkConfig.Provider?.Name ?? "default";
            var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
            var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

            _log.LogDebug("Environment {env}: Updating network {network}", savedNetwork.Environment ?? "default", savedNetwork.Name);

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

                    await _stateStore.LoadCollectionAsync(port, x => x.IpAssignments);
                    port.IpAssignments.Clear();
                }

                continue;
            }

            if(string.IsNullOrWhiteSpace(networkConfig.Address))
                throw new InconsistentNetworkConfigException($"Network '{networkConfig.Name}': Network address not set.");

            var networkAddress = IPNetwork2.Parse(networkConfig.Address);

            var providerPorts = savedNetwork.NetworkPorts
                .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

            //remove all ports if more than one
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
                    _log.LogInformation("Environment {env}, Network {network}: network router ip assignment changed to {ipAddress}.",
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
                _log.LogDebug("Removing subnets: {@removeSubnets}", (object)removeSubnets);

            await _stateStore.For<VirtualNetworkSubnet>().DeleteRangeAsync(removeSubnets);

            foreach (var subnetConfig in networkConfig.Subnets.DistinctBy(x => x.Name))
            {
                var savedSubnet = savedNetwork.Subnets.FirstOrDefault(x => x.Name == subnetConfig.Name)
                                  ?? throw new InconsistentNetworkConfigException($"Subnet {subnetConfig} not found in network {savedNetwork.Name}");

                _log.LogDebug("Updating subnet {network}/{subnet}", savedNetwork.Name, savedSubnet.Name);


                savedSubnet.DhcpLeaseTime = 3600;
                savedSubnet.MTU = subnetConfig.Mtu.GetValueOrDefault() == 0 ? 1400 : subnetConfig.Mtu.GetValueOrDefault();
                savedSubnet.DnsServersV4 = subnetConfig.DnsServers != null
                    ? string.Join(',', subnetConfig.DnsServers)
                    : null;
                savedSubnet.IpNetwork = subnetConfig.Address ?? networkConfig.Address;
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
                        _log.LogDebug("Updating ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
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
                        _log.LogDebug("creating new ip pool {network}/{subnet}/{pool}", savedNetwork.Name,
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
                await _stateStore.For<IpPool>().DeleteRangeAsync(removeIpPools);
            }
        }

    }

}