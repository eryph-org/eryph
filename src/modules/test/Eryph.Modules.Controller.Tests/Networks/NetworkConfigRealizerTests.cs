using Ardalis.Specification;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;
using JetBrains.Annotations;
using MartinCostello.Logging.XUnit;
using Moq;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Xunit;
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
                                providerPort.ProviderName.Should().Be(providerName);
                                providerPort.SubnetName.Should().Be(providerSubnetName);
                                providerPort.PoolName.Should().Be(providerPoolName);
                                providerPort.IpAssignments.Should().SatisfyRespectively(
                                    ipAssignment => ipAssignment.IpAddress.Should().Be(expectedIpAddress));
                            });
                    network.NetworkPorts.OfType<NetworkRouterPort>().Should().SatisfyRespectively(
                        routerPort =>
                        {
                            routerPort.Name.Should().Be("default");
                            routerPort.IpAssignments.Should().SatisfyRespectively(
                                ipAssignment => ipAssignment.IpAddress.Should().Be("10.0.100.1"));
                        });
                });
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

                    // TODO Is this correct or do we want to have a provider port?
                    network.NetworkPorts.Should().BeEmpty();
                });
        });
    }


    [Fact]
    public async Task Existing_ip_pool_is_updated()
    {

        var logger = new XUnitLogger("log", _testOutput, new XUnitLoggerOptions());

        var routerPort = new NetworkRouterPort()
        {
            Name = "default",
            MacAddress = "42:00:42:00:10:10",
            IpAssignments = new List<IpAssignment>
            {
                new IpPoolAssignment
                {
                    IpAddress = "192.168.0.10"
                }
            }
        };

        var network = new VirtualNetwork()
        {
            Id = new Guid(),
            Name = "test",
            Environment = EryphConstants.DefaultEnvironmentName,
            NetworkProvider = EryphConstants.DefaultProviderName,
            Subnets = new List<VirtualNetworkSubnet>
            {
                new()
                {
                    Name = "default",
                    IpNetwork = "",
                    IpPools = new List<IpPool>
                    {
                        new()
                        {
                            Name = "default",
                            IpNetwork = "10.0.0.0/22"
                        }
                    }
                }
            },
            RouterPort = routerPort,
            NetworkPorts = new List<VirtualNetworkPort>
            {
                new ProviderRouterPort()
                {
                    Name = "test-provider-port",
                    MacAddress = "42:00:42:00:00:10",
                    SubnetName = "test-provider-subnet",
                    PoolName = "test-provider-pool",
                },
                routerPort
            }
        };

        var stateStore = new Mock<IStateStore>();
        var networkRepo = new Mock<IStateStoreRepository<VirtualNetwork>>();

        networkRepo.Setup(x => x.ListAsync(It.IsAny<ISpecification<VirtualNetwork>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { network }.ToList);

        var subnetRepo = new Mock<IStateStoreRepository<VirtualNetworkSubnet>>();
        var ipPoolRepo = new Mock<IStateStoreRepository<IpPool>>();


        stateStore.Setup(x => x.For<VirtualNetwork>()).Returns(networkRepo.Object);
        stateStore.Setup(x => x.For<VirtualNetworkSubnet>()).Returns(subnetRepo.Object);
        stateStore.Setup(x => x.For<IpPool>()).Returns(ipPoolRepo.Object);

        var projectId = new Guid();
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks = new[]
            {
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.0.0/22",
                    Subnets = new[]
                    {
                        new NetworkSubnetConfig
                        {
                            Name = "default",
                            IpPools = new[]
                            {
                                new IpPoolConfig
                                {
                                    Name = "default",
                                    FirstIp = "10.0.0.50",
                                    LastIp = "10.0.0.200",
                                }
                            }
                        }
                    }
                }
            }
        };

        var networkProviderConfig = new NetworkProvidersConfiguration()
        {
            NetworkProviders = new NetworkProvider[]
            {
                new()
                {
                    Name = "default",
                    Subnets =
                        new[]
                        {
                            new NetworkProviderSubnet
                            {
                                Name = "default",
                                Network = "10.0.0.0/24",
                                Gateway = "10.0.0.1"

                            }
                        }
                }
            }
        };

        var realizer = new NetworkConfigRealizer(stateStore.Object, Mock.Of<IIpPoolManager>(), logger);
        await realizer.UpdateNetwork(projectId, networkConfig, networkProviderConfig);

        network.Subnets![0].IpPools![0].FirstIp.Should().Be("10.0.0.50");
        network.Subnets![0].IpPools![0].LastIp.Should().Be("10.0.0.200");

    }


    [Fact]
    public async Task Cleanup_of_overlay_when_switched_to_flat()
    {

        var logger = new XUnitLogger("log", _testOutput, new XUnitLoggerOptions());

        var routerPort = new NetworkRouterPort()
        {
            Name = "default",
            MacAddress = "42:00:42:00:00:10",
            IpAssignments = new List<IpAssignment>
            {
                new IpPoolAssignment
                {
                    IpAddress = "192.168.0.10"
                }
            }
        };

        var network = new VirtualNetwork()
        {
            Id = new Guid(),
            Name = "test",
            Environment = EryphConstants.DefaultEnvironmentName,
            NetworkProvider = EryphConstants.DefaultProviderName,
            Subnets = new List<VirtualNetworkSubnet>
            {
                new()
                {
                    Name = "default",
                    IpNetwork = "",
                    IpPools = new List<IpPool>
                    {
                        new()
                        {
                            Name = "default",
                            IpNetwork = "10.0.0.0/22"
                        }
                    }
                }
            },
            RouterPort = routerPort,
            NetworkPorts = new List<VirtualNetworkPort>
            {
                new ProviderRouterPort
                {
                    Name = "test-provider-port",
                    MacAddress = "00:00:00:00:10:01",
                    SubnetName = "test-provider-subnet",
                    PoolName = "test-provider-pool",
                    IpAssignments =
                    [
                        new IpPoolAssignment
                        {
                            IpAddress = "192.168.0.10"
                        }
                    ]
                },
                routerPort
            }
        };

        var stateStore = new Mock<IStateStore>();
        var networkRepo = new Mock<IStateStoreRepository<VirtualNetwork>>();

        networkRepo.Setup(x => x.ListAsync(It.IsAny<ISpecification<VirtualNetwork>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { network }.ToList);

        var subnetRepo = new Mock<IStateStoreRepository<VirtualNetworkSubnet>>();
        var ipPoolRepo = new Mock<IStateStoreRepository<IpPool>>();


        stateStore.Setup(x => x.For<VirtualNetwork>()).Returns(networkRepo.Object);
        stateStore.Setup(x => x.For<VirtualNetworkSubnet>()).Returns(subnetRepo.Object);
        stateStore.Setup(x => x.For<IpPool>()).Returns(ipPoolRepo.Object);

        var projectId = new Guid();
        var networkConfig = new ProjectNetworksConfig()
        {
            Networks = new[]
            {
                new NetworkConfig
                {
                    Name = "test",
                    Address = "10.0.0.0/22",
                    Subnets = new[]
                    {
                        new NetworkSubnetConfig
                        {
                            Name = "default",
                            IpPools = new[]
                            {
                                new IpPoolConfig
                                {
                                    Name = "default",
                                    FirstIp = "10.0.0.50",
                                    LastIp = "10.0.0.200",
                                }
                            }
                        }
                    }
                }
            }
        };

        var networkProviderConfig = new NetworkProvidersConfiguration()
        {
            NetworkProviders = new NetworkProvider[]
            {
                new()
                {
                    Name = "default",
                    TypeString = "flat"
                }
            }
        };

        var realizer = new NetworkConfigRealizer(stateStore.Object, Mock.Of<IIpPoolManager>(), logger);
        await realizer.UpdateNetwork(projectId, networkConfig, networkProviderConfig);

        network.Subnets.Should().HaveCount(0);
        routerPort.IpAssignments.Should().HaveCount(0);

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
