using Ardalis.Specification;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using MartinCostello.Logging.XUnit;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public class NetworkConfigRealizerTests
    {
        private readonly ITestOutputHelper _testOutput;

        public NetworkConfigRealizerTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task Creates_new_overlay_network()
        {
            var logger = new XUnitLogger("log", _testOutput, new XUnitLoggerOptions());

            var addedNetworks = new List<VirtualNetwork>();
            var stateStore = new Mock<IStateStore>();
            var networkRepo = new Mock<IStateStoreRepository<VirtualNetwork>>();
            networkRepo.Setup(x => x.AddAsync(
                    It.IsAny<VirtualNetwork>(), It.IsAny<CancellationToken>()))
                .Returns((VirtualNetwork n, CancellationToken _) =>
                {
                    addedNetworks.Add(n);
                    return Task.FromResult(n);
                });

            networkRepo.Setup(x => x.ListAsync(It.IsAny<ISpecification<VirtualNetwork>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<VirtualNetwork>().ToList);

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
                        Address = "10.0.0.0/22"

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

            addedNetworks.Should().HaveCount(1);
            addedNetworks[0].Name.Should().Be("test");
            addedNetworks[0].Subnets.Should().HaveCount(1);
            addedNetworks[0].Subnets[0].Name.Should().Be("default");
            addedNetworks[0].Subnets[0].IpNetwork.Should().Be("10.0.0.0/22");
            addedNetworks[0].Subnets[0].IpPools.Should().HaveCount(1);
            addedNetworks[0].Subnets[0].IpPools[0].Name.Should().Be("default");
            addedNetworks[0].Subnets[0].IpPools[0].IpNetwork.Should().Be("10.0.0.0/22");
            addedNetworks[0].Subnets[0].IpPools[0].FirstIp.Should().Be("10.0.0.2");

            addedNetworks[0].NetworkPorts.Should().HaveCount(2);
            addedNetworks[0].NetworkPorts[0].Should().BeOfType<ProviderRouterPort>();
            addedNetworks[0].NetworkPorts[1].Should().BeOfType<NetworkRouterPort>();


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
    }
}