using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using VirtualNetwork = Eryph.StateDb.Model.VirtualNetwork;

namespace Eryph.Modules.ComputeApi.Handlers
{
    internal class GetVirtualNetworksConfigurationHandler : IGetRequestHandler<Project, 
        VirtualNetworkConfiguration>
    {
        private readonly IStateStore _stateStore;

        public GetVirtualNetworksConfigurationHandler(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task<ActionResult<VirtualNetworkConfiguration>> HandleGetRequest(Func<ISingleResultSpecification<Project>> specificationFunc, CancellationToken cancellationToken)
        {
            var projectSpec = specificationFunc();

            var project= await _stateStore.Read<Project>().GetBySpecAsync(projectSpec, cancellationToken);

            if (project == null)
                return new NotFoundResult();
            
            var networks = await _stateStore.For<VirtualNetwork>().ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), cancellationToken);

            var projectConfig = new ProjectNetworksConfig
            {
                Project = project.Name, Version = "1.0",
                Networks = networks.Map(network =>
                {
                    var networkConfig = new NetworkConfig
                    {
                        Name = network.Name,
                        Address = network.IpNetwork,
                        Provider = new ProviderConfig
                        {
                            Name = network.NetworkProvider != "default" ? network.NetworkProvider: null
                        },

                        Subnets = network.Subnets.Count > 0 ? network.Subnets.Map(subnet =>
                        {

                            var ipNetwork = IPNetwork.Parse(subnet.IpNetwork);

                            return new NetworkSubnetConfig
                            {
                                Name = subnet.Name,
                                Address = subnet.IpNetwork != network.IpNetwork ? subnet.IpNetwork: null,
                                DnsServers = ipNetwork.AddressFamily == AddressFamily.InterNetwork
                                    ? subnet.DnsServersV4?.Split(",", StringSplitOptions.RemoveEmptyEntries)
                                    : null,
                                Mtu = subnet.MTU,
                                IpPools = subnet.IpPools.Count > 0 ? subnet.IpPools.Select(ipPool => new IpPoolConfig
                                {
                                    Name = ipPool.Name,
                                    FirstIp = ipPool.FirstIp,
                                    LastIp = ipPool.LastIp
                                }).ToArray(): null

                            };
                        }).ToArray(): null

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
                }).ToArray()
            };

            var configString = ConfigModelJsonSerializer.Serialize(projectConfig);

            var result = new VirtualNetworkConfiguration()
            {
                Configuration = JsonSerializer.Deserialize<JsonElement>(configString)
            };

            return result;
        }
    }
}
