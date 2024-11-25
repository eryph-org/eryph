using Ardalis.Specification;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks;

public class NetworkConfigRealizerTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private readonly NetworkProvidersConfiguration _networkProvidersConfig = new()
    {
        NetworkProviders =
        [
            new NetworkProvider
            {
                Name = "default",
                TypeString = "nat_overlay",
                BridgeName = "br-nat",
                Subnets =
                [
                    new NetworkProviderSubnet
                    {
                        Name = "default",
                        Network = "10.249.248.0/24",
                        Gateway = "10.249.248.1",
                        IpPools =
                        [
                            new NetworkProviderIpPool
                            {
                                Name = "default",
                                FirstIp = "10.249.248.10",
                                NextIp = "10.249.248.12",
                                LastIp = "10.249.248.19"
                            },
                            new NetworkProviderIpPool
                            {
                                Name = "second-provider-pool",
                                FirstIp = "10.249.248.20",
                                NextIp = "10.249.248.22",
                                LastIp = "10.249.248.29"
                            },
                        ],
                    },
                    new NetworkProviderSubnet
                    {
                        Name = "second-provider-subnet",
                        Network = "10.249.249.0/24",
                        Gateway = "10.249.249.1",
                        IpPools =
                        [
                            new NetworkProviderIpPool
                            {
                                Name = "default",
                                FirstIp = "10.249.249.10",
                                NextIp = "10.249.249.12",
                                LastIp = "10.249.249.19"
                            },
                        ],
                    },
                ],
            },
            new NetworkProvider
            {
                Name = "second-overlay-provider",
                TypeString = "nat_overlay",
                BridgeName = "br-second-nat",
                Subnets =
                [
                    new NetworkProviderSubnet
                    {
                        Name = "default",
                        Network = "10.249.250.0/24",
                        Gateway = "10.249.250.1",
                        IpPools =
                        [
                            new NetworkProviderIpPool
                            {
                                Name = "default",
                                FirstIp = "10.249.250.10",
                                NextIp = "10.249.250.12",
                                LastIp = "10.249.250.19"
                            },
                        ],
                    },
                ],
            },
            new NetworkProvider
            {
                Name = "flat-provider",
                TypeString = "flat",
            },
        ]
    };

    private readonly ITestOutputHelper _testOutput = outputHelper;

    [Theory]
    [InlineData("default", "default", "default", "10.249.248.12")]
    [InlineData("default", "default", "second-provider-pool", "10.249.248.22")]
    [InlineData("default", "second-provider-subnet", "default", "10.249.249.12")]
    [InlineData("second-overlay-provider", "default", "default", "10.249.250.12")]
    public async Task UpdateNetwork_NewNetworkWithOverlayProvider_UsesCorrectProvider(
        string providerName,
        string providerSubnetName,
        string providerPoolName,
        string expectedIpAddress)
    {
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Provider = new ProviderConfig()
                    {
                        Name = providerName,
                        Subnet = providerSubnetName,
                        IpPool = providerPoolName,
                    },
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network => AssertOverlayNetwork(
                    network,
                    providerName,
                    providerSubnetName,
                    providerPoolName,
                    expectedIpAddress));
        });
    }

    [Fact]
    public async Task UpdateNetwork_NewNetworkWithFlatProvider_UsesCorrectProvider()
    {
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Provider = new ProviderConfig()
                    {
                        Name = "flat-provider",
                    },
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network =>
                {
                    network.Name.Should().Be("test");

                    // TODO Is this correct or do we want to have a provider port?
                    network.Subnets.Should().BeEmpty();
                    network.NetworkPorts.Should().BeEmpty();
                });
        });
    }

    [Fact]
    public async Task UpdateNetwork_IpRangeOfExistingPoolIsChanged_ExistingPoolIsUpdated()
    {
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        Guid ipPoolId = Guid.Empty;
        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network => AssertOverlayNetwork(
                    network,
                    EryphConstants.DefaultProviderName,
                    EryphConstants.DefaultSubnetName,
                    EryphConstants.DefaultIpPoolName,
                    "10.249.248.12"));

            ipPoolId = networks.Should().ContainSingle()
                .Which.Subnets.Should().ContainSingle()
                .Which.IpPools.Should().ContainSingle()
                .Which.Id;
        });

        var updatedNetworkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Subnets = 
                    [
                        new NetworkSubnetConfig()
                        {
                            Name = EryphConstants.DefaultSubnetName,
                            IpPools =  
                            [
                                new IpPoolConfig()
                                {
                                    Name = EryphConstants.DefaultIpPoolName,
                                    FirstIp = "10.0.100.2",
                                    NextIp = "10.0.100.10",
                                    LastIp = "10.0.100.100",
                                },
                            ],
                        }
                    ]
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, updatedNetworkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network =>
                {
                    network.Name.Should().Be("test");

                    network.Subnets.Should().SatisfyRespectively(
                        subnet =>
                        {
                            subnet.Name.Should().Be("default");
                            subnet.IpNetwork.Should().Be("10.0.100.0/22");

                            subnet.IpPools.Should().SatisfyRespectively(
                                pool =>
                                {
                                    pool.Id.Should().Be(ipPoolId);
                                    pool.Name.Should().Be("default");
                                    pool.IpNetwork.Should().Be("10.0.100.0/22");
                                    pool.FirstIp.Should().Be("10.0.100.2");
                                    pool.NextIp.Should().Be("10.0.100.10");
                                    pool.LastIp.Should().Be("10.0.100.100");
                                });
                        });
                });
        });
    }

    [Fact]
    public async Task UpdateNetwork_ChangeNetworkFromOverlayToFlat_RemovesOldOverlayConfiguration()
    {
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network => AssertOverlayNetwork(
                    network,
                    EryphConstants.DefaultProviderName,
                    EryphConstants.DefaultSubnetName,
                    EryphConstants.DefaultIpPoolName,
                    "10.249.248.12"));
        });

        var updatedNetworkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Provider = new ProviderConfig()
                    {
                        Name = "flat-provider",
                    },
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, updatedNetworkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network =>
                {
                    network.Name.Should().Be("test");

                    // TODO Is this correct or do we want to have a provider port?
                    network.Subnets.Should().BeEmpty();
                    network.NetworkPorts.Should().BeEmpty();
                });

            var providerPorts = await stateStore.For<ProviderRouterPort>().ListAsync();
            providerPorts.Should().BeEmpty();

            var networkRouterPorts = await stateStore.For<NetworkRouterPort>().ListAsync();
            networkRouterPorts.Should().BeEmpty();

            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().BeEmpty();
        });
    }

    [Theory]
    [InlineData("default", "default", "second-provider-pool", "10.249.248.22")]
    [InlineData("default", "second-provider-subnet", "default", "10.249.249.12")]
    [InlineData("second-overlay-provider", "default", "default", "10.249.250.12")]
    public async Task UpdateNetwork_ChangeNetworkToDifferentOverlayProvider_UpdatesConfiguration(
        string providerName,
        string providerSubnetName,
        string providerPoolName,
        string expectedIpAddress)
    {
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Provider = new ProviderConfig()
                    {
                        Name = EryphConstants.DefaultProviderName,
                        Subnet = EryphConstants.DefaultSubnetName,
                        IpPool = EryphConstants.DefaultIpPoolName,
                    },
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network => AssertOverlayNetwork(
                    network,
                    EryphConstants.DefaultProviderName,
                    EryphConstants.DefaultSubnetName,
                    EryphConstants.DefaultIpPoolName,
                    "10.249.248.12"));
        });

        var updatedNetworkConfig = new ProjectNetworksConfig()
        {
            Networks =
            [
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.100.0/22",
                    Provider = new ProviderConfig()
                    {
                        Name = providerName,
                        Subnet = providerSubnetName,
                        IpPool = providerPoolName,
                    },
                },
            ],
        };

        await WithScope(async (realizer, stateStore) =>
        {
            await realizer.UpdateNetwork(EryphConstants.DefaultProjectId, updatedNetworkConfig, _networkProvidersConfig);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var networks = await stateStore.For<VirtualNetwork>().ListAsync(new GetAllNetworks());

            networks.Should().SatisfyRespectively(
                network => AssertOverlayNetwork(
                    network,
                    providerName,
                    providerSubnetName,
                    providerPoolName,
                    expectedIpAddress));

            var providerPorts = await stateStore.For<ProviderRouterPort>().ListAsync();
            providerPorts.Should().SatisfyRespectively(
                port =>
                {
                    port.SubnetName.Should().Be(providerSubnetName);
                    port.PoolName.Should().Be(providerPoolName);
                });

            var networkRouterPorts = await stateStore.For<NetworkRouterPort>().ListAsync();
            networkRouterPorts.Should().HaveCount(1);

            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().Satisfy(
                assignment => assignment.IpAddress == expectedIpAddress,
                assignment => assignment.IpAddress == "10.0.100.1");
        });
    }

    private void AssertOverlayNetwork(
        VirtualNetwork network,
        string expectedProviderName,
        string expectedProviderSubnet,
        string expectedProviderPool,
        string expectedProviderIp)
    {
        network.Name.Should().Be("test");

        network.Subnets.Should().SatisfyRespectively(
            subnet =>
            {
                subnet.Name.Should().Be("default");
                subnet.IpNetwork.Should().Be("10.0.100.0/22");

                subnet.IpPools.Should().SatisfyRespectively(
                    pool =>
                    {
                        pool.Name.Should().Be("default");
                        pool.IpNetwork.Should().Be("10.0.100.0/22");
                        pool.FirstIp.Should().Be("10.0.100.2");
                        pool.NextIp.Should().Be("10.0.100.2");
                        pool.LastIp.Should().Be("10.0.103.254");
                    });
            });

        network.NetworkPorts.Should().HaveCount(2);
        network.NetworkPorts.OfType<ProviderRouterPort>().Should().SatisfyRespectively(
                providerPort =>
                {
                    providerPort.Name.Should().Be("provider");
                    providerPort.ProviderName.Should().Be(expectedProviderName);
                    providerPort.SubnetName.Should().Be(expectedProviderSubnet);
                    providerPort.PoolName.Should().Be(expectedProviderPool);
                    providerPort.IpAssignments.Should().SatisfyRespectively(
                        ipAssignment => ipAssignment.IpAddress.Should().Be(expectedProviderIp));
                });
        network.NetworkPorts.OfType<NetworkRouterPort>().Should().SatisfyRespectively(
            routerPort =>
            {
                routerPort.Name.Should().Be("default");
                routerPort.IpAssignments.Should().SatisfyRespectively(
                    ipAssignment => ipAssignment.IpAddress.Should().Be("10.0.100.1"));
            });
    }

    private async Task WithScope(Func<INetworkConfigRealizer, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var realizer = scope.GetInstance<INetworkConfigRealizer>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(realizer, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        // Use the proper manager instead of a mock. The code is quite
        // interdependent as it modifies the same EF Core entities.
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);

        options.Container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        var configRealizer = new NetworkProvidersConfigRealizer(stateStore);
        await configRealizer.RealizeConfigAsync(_networkProvidersConfig, default);
    }

    private sealed class GetAllNetworks : Specification<VirtualNetwork>
    {
        public GetAllNetworks()
        {
            Query.Include(x => x.NetworkPorts)
                .ThenInclude(p => p.FloatingPort)
                .Include(n => n.NetworkPorts)
                .ThenInclude(p => p.IpAssignments)
                .ThenInclude(a => ((IpPoolAssignment)a).Pool)
                .Include(n => n.NetworkPorts)
                .ThenInclude(p => p.IpAssignments)
                .ThenInclude(a => a.Subnet)
                .Include(x => x.Subnets)
                .Include(x => x.RouterPort)
                .ThenInclude(x => x!.FloatingPort)
                .Include(x => x.Subnets)
                .ThenInclude(x => x.IpPools);
        }
    }
}
