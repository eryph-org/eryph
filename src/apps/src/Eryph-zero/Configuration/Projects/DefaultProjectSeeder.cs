using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    [UsedImplicitly]
    internal class DefaultProjectSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly ILogger _logger;
        private readonly INetworkProviderManager _networkProviderManager;
        private readonly IStateStore _stateStore;

        public DefaultProjectSeeder(
            ILogger logger,
            INetworkProviderManager networkProviderManager,
            IStateStore stateStore)
        {
            _logger = logger;
            _networkProviderManager = networkProviderManager;
            _stateStore = stateStore;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            var tenantId = EryphConstants.DefaultTenantId;

            var networkProvider = (await _networkProviderManager.GetCurrentConfiguration().IfLeft(_ =>
                new NetworkProvidersConfiguration
                {
                    NetworkProviders = Array.Empty<NetworkProvider>()
                }).Select(x => x.NetworkProviders)).FirstOrDefault(x=>x.Name == "default") 
                ?? new NetworkProvider{ Name = "default" };


            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetByName(tenantId, "default")
                , stoppingToken);

            if (project == null)
            {
                _logger.LogInformation("Default project '{projectId}' not found in state db. Creating project record.", EryphConstants.DefaultProjectId);

                project = new Project
                {
                    Id = EryphConstants.DefaultProjectId,
                    Name = "default",
                    TenantId = tenantId
                };
                await _stateStore.For<Project>().AddAsync(project, stoppingToken);
            }

            await _stateStore.For<Project>().SaveChangesAsync(stoppingToken);
            var projectId = project.Id;

            var network = await _stateStore.For<VirtualNetwork>().GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(projectId, "default", "default")
                , stoppingToken);


            if (network == null)
            {
                _logger.LogInformation("Default network not found in state db. Creating network record.");

                var networkId = Guid.NewGuid();

                if (networkProvider.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay)
                {
                    var routerPort = new NetworkRouterPort
                    {
                        Id = Guid.NewGuid(),
                        MacAddress = "d2:e7:a7:37:40:f9",
                        IpAssignments = new List<IpAssignment>(new[]
                        {
                            new IpAssignment
                            {
                                Id = Guid.NewGuid(),
                                IpAddress = "10.0.0.1",
                            }
                        }),
                        Name = "default",
                        RoutedNetworkId = networkId,
                        NetworkId = networkId,
                    };

                    network = new VirtualNetwork
                    {
                        Id = networkId,
                        Name = "default",
                        Environment = "default",
                        ProjectId = projectId,
                        IpNetwork = "10.0.0.0/20",
                        NetworkProvider = "default",
                        RouterPort = routerPort,
                        NetworkPorts = new List<VirtualNetworkPort>
                        {
                            routerPort,
                            new ProviderRouterPort()
                            {
                                Name = "provider",
                                Id = Guid.NewGuid(),
                                ProviderName = "default",
                                SubnetName = "default",
                                PoolName = "default",
                                MacAddress = "d2:e7:a7:37:40:f8"
                            }

                        },
                        Subnets = new List<VirtualNetworkSubnet>(new[]
                        {
                            new VirtualNetworkSubnet
                            {
                                Id = Guid.NewGuid(),
                                IpNetwork = "10.0.0.0/20",
                                Name = "default",
                                DhcpLeaseTime = 3600,
                                MTU = 1400,
                                DnsServersV4 = "9.9.9.9,8.8.8.8",
                                IpPools = new List<IpPool>(new[]
                                {
                                    new IpPool
                                    {
                                        Id = Guid.NewGuid(),
                                        Name = "default",
                                        IpNetwork = "10.0.0.0/20",
                                        Counter = 0,
                                        FirstIp = "10.0.0.100",
                                        LastIp = "10.0.2.240"
                                    }
                                })
                            }
                        })
                    };
                }
                else
                {
                    network = new VirtualNetwork
                    {
                        Id = networkId,
                        Name = "default",
                        ProjectId = projectId,
                        NetworkProvider = "default",
                    };
                }

                await _stateStore.For<VirtualNetwork>().AddAsync(network, stoppingToken);
            }

            await _stateStore.For<VirtualNetwork>().SaveChangesAsync(stoppingToken);
        }
    }
}