using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.ZeroState.VirtualNetworks;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ZeroState.Tests.VirtualNetworks
{
    public class ZeroStateVirtualNetworkChangeInterceptorTests : ZeroStateTestBase
    {
        private static readonly Guid NetworkId = Guid.NewGuid();
        private static readonly Guid SubnetId = Guid.NewGuid();
        private static readonly Guid IpPoolId = Guid.NewGuid();

        [Fact]
        public async Task IpPool_update_is_detected()
        {
            using var host = CreateHost();
            await host.StartAsync();
            await using (var scope = AsyncScopedLifestyle.BeginScope(host.Services.GetRequiredService<Container>()))
            {
                var stateStore = scope.GetInstance<IStateStore>();
                var pool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
                pool!.NextIp = "10.0.0.111";
                await stateStore.SaveChangesAsync();
            }
            await host.StopAsync();

            var config = await ReadConfig();
            config.Should().BeEquivalentTo(new ProjectNetworksConfig()
            {
                Version = "1.0",
                Project = "default",
                Networks =
                [
                    new NetworkConfig()
                    {
                        Name = "Test Network",
                        Subnets =
                        [
                            new NetworkSubnetConfig()
                            {
                                Name = "Test Subnet",
                                Address = "10.0.0.0/20",
                                Mtu = 1400,
                                IpPools =
                                [
                                    new IpPoolConfig()
                                    {
                                        Name = "Test Pool",
                                        FirstIp = "10.0.0.100",
                                        NextIp = "10.0.0.111",
                                        LastIp = "10.0.0.200",
                                    },
                                ],
                            },
                        ],
                    },
                ],
            });
        }

        private async Task<ProjectNetworksConfig> ReadConfig()
        {
            var path = Path.Combine(
                ZeroStateConfig.ProjectNetworksConfigPath,
                $"{EryphConstants.DefaultProjectId}.json");
            var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);

            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
            return ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
        }

        protected override async Task SeedAsync(IStateStore stateStore)
        {
            await SeedDefaultTenantAndProject();

            var network = new VirtualNetwork()
            {
                Id = NetworkId,
                Name = "Test Network",
                ProjectId = EryphConstants.DefaultProjectId,
                Subnets =
                [
                    new VirtualNetworkSubnet()
                    {
                        Id = SubnetId,
                        Name = "Test Subnet",
                        IpNetwork = "10.0.0.0/20",
                        MTU = 1400,
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = IpPoolId,
                                Name = "Test Pool",
                                FirstIp = "10.0.0.100",
                                NextIp = "10.0.0.110",
                                LastIp = "10.0.0.200",
                            },
                        ],
                    },
                ],
            };

            await stateStore.For<VirtualNetwork>().AddAsync(network);
        }
    }
}
