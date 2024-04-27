using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using Eryph.ConfigModel.Networks;
using Eryph.StateDb.Model;

namespace Eryph.ModuleCore.Networks
{
    public static class VirtualNetworksExtensions
    {
        public static ProjectNetworksConfig ToNetworksConfig(this IEnumerable<VirtualNetwork> networks,
            string projectName)
        {
            return new ProjectNetworksConfig
            {
                Project = projectName,
                Version = "1.0",
                Networks = networks.Map(network =>
                {
                    var networkConfig = new NetworkConfig
                    {
                        Name = network.Name,
                        Environment = network.Environment != "default" ? network.Environment : null,
                        Address = network.IpNetwork,
                        Provider = new ProviderConfig
                        {
                            Name = network.NetworkProvider != "default" ? network.NetworkProvider : null
                        },

                        Subnets = network.Subnets.Count > 0 ? network.Subnets.Map(subnet =>
                        {

                            var ipNetwork = IPNetwork2.Parse(subnet.IpNetwork);

                            return new NetworkSubnetConfig
                            {
                                Name = subnet.Name,
                                Address = subnet.IpNetwork != network.IpNetwork ? subnet.IpNetwork : null,
                                DnsServers = GetDnsServers(ipNetwork, subnet),
                                DnsDomain = GetDnsDomain(network, subnet),
                                Mtu = subnet.MTU,
                                IpPools = subnet.IpPools.Count > 0 ? subnet.IpPools.Select(ipPool => new IpPoolConfig
                                {
                                    Name = ipPool.Name,
                                    FirstIp = ipPool.FirstIp,
                                    LastIp = ipPool.LastIp,
                                    NextIp = ipPool.NextIp,
                                }).ToArray() : null
                            };
                        }).ToArray() : null

                    };

                    foreach (var networkPort in network.NetworkPorts)
                    {
                        // ports are currently only used to lookup config of provider - later we have to add static assigned ports
                        if (networkPort is not ProviderRouterPort providerRouterPort) continue;

                        networkConfig.Provider.IpPool = providerRouterPort.PoolName != "default" ? providerRouterPort.PoolName : null;
                        networkConfig.Provider.Subnet = providerRouterPort.SubnetName != "default" ? providerRouterPort.SubnetName : null;
                        break;

                    }


                    // reduce if only defaults
                    if (networkConfig.Provider.Name == null && networkConfig.Provider.IpPool == null && networkConfig.Provider.Subnet == null)
                        networkConfig.Provider = null;


                    return networkConfig;
                }).OrderBy(x=>x.Environment ?? "A").ThenBy(x=>x.Name).ToArray()
            };

            string? GetDnsDomain(VirtualNetwork network, Subnet subnet)
            {
                // set to null in case it matches either the default or environment default
                if (network.Environment is null or "default")
                    return subnet.DnsDomain == "home.arpa" ? null : subnet.DnsDomain;

                return subnet.DnsDomain == $"{network.Environment ?? "".ToLowerInvariant()}.home.arpa"
                    ? null
                    : subnet.DnsDomain;
            }

            string[]? GetDnsServers(IPNetwork2 ipNetwork, VirtualNetworkSubnet subnet)
            {
                return ipNetwork.AddressFamily == AddressFamily.InterNetwork
                    ? subnet.DnsServersV4?.Split(",", StringSplitOptions.RemoveEmptyEntries)
                    : null;
            }
        }
    }
}
