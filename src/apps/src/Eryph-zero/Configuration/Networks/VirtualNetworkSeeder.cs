using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core.Network;
using Eryph.Core;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Eryph.ConfigModel.Networks;
using Eryph.Modules.Controller.Networks;
using LanguageExt.Common;

namespace Eryph.Runtime.Zero.Configuration.Networks
{
    internal class VirtualNetworkSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly ILogger _logger;
        private readonly INetworkProviderManager _networkProviderManager;
        private readonly IConfigReader<ProjectNetworksConfig> _configReader;
        private readonly INetworkConfigRealizer _configRealizer;
        private readonly IStateStore _stateStore;

        public VirtualNetworkSeeder(
            ILogger logger,
            INetworkProviderManager networkProviderManager,
            IConfigReader<ProjectNetworksConfig> configReader,
            INetworkConfigRealizer configRealizer,
            IStateStore stateStore)
        {
            _logger = logger;
            _networkProviderManager = networkProviderManager;
            _configReader = configReader;
            _configRealizer = configRealizer;
            _stateStore = stateStore;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            await foreach (var config in _configReader.ReadAsync(stoppingToken))
            {
                var project = await _stateStore.For<Project>().GetBySpecAsync(new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, config.Project), stoppingToken);
                if (project is null)
                {
                    _logger.LogWarning("Could not find project {ProjectName} during restore of networks", config.Project);
                    continue;
                }

                // TODO skip restore if project already has networks

                // TODO fix error handling
                var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
                    .IfLeft(l => l.ToErrorException().Rethrow<NetworkProvidersConfiguration>());
                await _configRealizer.UpdateNetwork(project.Id, config, providerConfig);
            }

            await EnsureDefaultNetwork(stoppingToken);
        }

        private async Task EnsureDefaultNetwork(CancellationToken stoppingToken)
        {
            var projectId = EryphConstants.DefaultProjectId;
            var networkProvider = (await _networkProviderManager.GetCurrentConfiguration().IfLeft(_ =>
                                      new NetworkProvidersConfiguration
                                      {
                                          NetworkProviders = Array.Empty<NetworkProvider>()
                                      }).Select(x => x.NetworkProviders)).FirstOrDefault(x => x.Name == "default")
                                  ?? new NetworkProvider { Name = "default" };

            var network = await _stateStore.For<VirtualNetwork>().GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(projectId, "default", "default"),
                stoppingToken);

            if (network is not null)
                return;
            
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
                        },
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

            // TODO use data update service
            await _stateStore.For<VirtualNetwork>().AddAsync(network, stoppingToken);
            await _stateStore.For<VirtualNetwork>().SaveChangesAsync(stoppingToken);
        }
    }
}
