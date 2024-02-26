using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Networks;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public sealed class CatletIpManagerTests : IDisposable
    {
        public void Dispose()
        {
            _connection.Dispose();
        }

        private readonly SqliteConnection _connection;

        public CatletIpManagerTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
        }

        
        [Theory]
        [InlineData(ProjectA, NetworkA_Default_Subnet, "default", null, null, null)]
        [InlineData(ProjectA, NetworkA_Other_Subnet, "default", "default", "other", null)]
        [InlineData(ProjectB, NetworkB_Default_Subnet, "default", "default", "default", "other")]
        [InlineData(ProjectB, NetworkB_Default_Subnet, "default", null, null, null)]
        [InlineData(ProjectB, NetworkB_Env2_Subnet, "env2", null, null, null)]
        [InlineData(ProjectA, NetworkA_Default_Subnet, "env2", null, null, null)]
        public async Task Adds_catlet_network_port_to_expected_pool(string projectIdString, string subnetIdString, 
            string environment, string? network, string? subnet, string? pool)
        {
            var networkConfig = new CatletNetworkConfig();
            if (network != null)
            {
                networkConfig.Name = network;
                if (subnet != null || pool!= null)
                    networkConfig.SubnetV4 = new CatletSubnetConfig
                    {
                        Name = subnet ?? "default",
                        IpPool = pool
                    };
            }

            var catletPort = new CatletNetworkPort
            {
                Id = Guid.NewGuid()
            };
            var projectId = Guid.Parse(projectIdString);
            var subnetId = Guid.Parse(subnetIdString);

            var contextOptions = new DbContextOptionsBuilder<StateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new StateStoreContext(contextOptions);
            var stateStore = new StateStore(context);
            await context.Database.EnsureCreatedAsync();

            var networkRepo = stateStore.For<VirtualNetwork>();
            foreach (var virtualNetwork in CreateNetworks())
            {
                await networkRepo.AddAsync(virtualNetwork);
            }

            await context.SaveChangesAsync();

            var poolManager = new Mock<IIpPoolManager>();
            poolManager.Setup(x => x.AcquireIp(subnetId,
                    pool ?? "default", It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, IpPoolAssignment>(new IpPoolAssignment
                {
                    IpAddress = "192.168.2.1"
                }))
                .Verifiable();

            try
            {
                var ipManager = new CatletIpManager(stateStore, poolManager.Object);
                var result = await ipManager.ConfigurePortIps(projectId,
                    environment, catletPort, new[] { networkConfig },
                    CancellationToken.None);

                var addresses = result.Should().BeRight().Subject;
                addresses.Should().HaveCount(1);
                addresses[0].ToString().Should().Be("192.168.2.1");
            }
            finally
            {
                poolManager.Verify();
            }

        }

        [Fact]
        public async Task Deletes_Invalid_Port()
        {
            var projectId = Guid.Parse(ProjectA);
            var networkConfig = new CatletNetworkConfig()
            {
                Name = "default"
            };
            
            var catletPort = new CatletNetworkPort
            {
                Id = Guid.NewGuid(),
                NetworkId = Guid.Parse(NetworkA_Default_Subnet),
                IpAssignments = new List<IpAssignment>
                {
                    new IpPoolAssignment()
                    {
                        SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                        Pool = new IpPool
                        {
                            SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                            Name = "other"
                        },
                    }
                }
            };

            var contextOptions = new DbContextOptionsBuilder<StateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new StateStoreContext(contextOptions);
            var ipManager = await SetupPortTest(context, catletPort);
            var stateStore = new StateStore(context);
            var port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);
            port!.IpAssignments.Should().HaveCount(1);

            var result = await ipManager.ConfigurePortIps(projectId,
                "default", catletPort, new[] { networkConfig },
                CancellationToken.None);

            result.Should().BeRight();

            await context.SaveChangesAsync();
            port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);

            port!.IpAssignments.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task Keeps_Valid_Port()
        {
            var projectId = Guid.Parse(ProjectA);
            var networkConfig = new CatletNetworkConfig()
            {
                Name = "default"
            };

            var catletPort = new CatletNetworkPort
            {
                Id = Guid.NewGuid(),
                NetworkId = Guid.Parse(NetworkA_Default_Subnet),
                IpAssignments = new List<IpAssignment>
                {
                    new IpPoolAssignment()
                    {
                        SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                        IpAddress = "192.168.2.10",
                        Pool = new IpPool
                        {
                            SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                            Name = "default"
                        },
                    }
                }
            };

            var contextOptions = new DbContextOptionsBuilder<StateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new StateStoreContext(contextOptions);
            var ipManager = await SetupPortTest(context, catletPort);
            var stateStore = new StateStore(context);
            var port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);
            port!.IpAssignments.Should().HaveCount(1);

            var result = await ipManager.ConfigurePortIps(projectId,
                "default", catletPort, new[] { networkConfig },
                CancellationToken.None);

            result.Should().BeRight().Subject.Should().HaveCount(1)
                .And.Subject.First().ToString().Should().Be("192.168.2.10");

        }

        private async Task<CatletIpManager> SetupPortTest(StateStoreContext context, CatletNetworkPort catletPort)
        {
            var subnetId = Guid.Parse(NetworkA_Default_Subnet);

            await context.Database.EnsureCreatedAsync();
            var stateStore = new StateStore(context);
            var networkRepo = stateStore.For<VirtualNetwork>();
            var network = CreateNetworks().First();
            network.NetworkPorts = new List<VirtualNetworkPort>
            {
                catletPort
            };
            await networkRepo.AddAsync(network);


            await context.SaveChangesAsync();

            var poolManager = new Mock<IIpPoolManager>();
            poolManager.Setup(x => x.AcquireIp(subnetId, "default",
                    It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, IpPoolAssignment>(new IpPoolAssignment
                {
                    IpAddress = "192.168.2.1"
                }));

            var ipManager = new CatletIpManager(stateStore, poolManager.Object);
            return ipManager;
        }

        // ReSharper disable InconsistentNaming
        private const string ProjectA = "{96BBD6D7-01F9-4001-8C86-3FBA75BAA1B5}";
        private const string NetworkA_Default = "{CB58FE00-3F64-4B66-B58E-23FB15DF3CAC}";
        private const string NetworkA_Default_Subnet = "{ED6697CD-836F-4DA7-914B-B09ED1567934}";
        private const string NetworkA_Other_Subnet = "{29FB8B37-4779-427A-BC5C-9A5ECCFFD5E2}";

        private const string ProjectB = "{75C27DAF-77C8-4B98-A072-A4706DCEB422}";
        private const string NetworkB_Default = "{A0CE4B1A-03E6-413B-A048-567079B49B28}";
        private const string NetworkB_Env2_Default = "{29408ED0-F876-4879-9FAA-DEB519D1DF0A}";
        private const string NetworkB_Default_Subnet = "{AC451FA5-3364-4593-AA4D-14F95529FD54}";
        private const string NetworkB_Env2_Subnet = "{91A25D95-F417-482D-9264-E4179F61E379}";
        // ReSharper restore InconsistentNaming


        private static IEnumerable<VirtualNetwork> CreateNetworks()
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
            };

            var projectA = new Project
            {
                Id = Guid.Parse(ProjectA),
                Name = "projectA",
                Tenant = tenant
            };

            var projectB = new Project
            {
                Id = Guid.Parse(ProjectB),
                Name = "projectB",
                Tenant = tenant
            };

            return new[]
            {
                new VirtualNetwork
                {
                    Id = Guid.Parse(NetworkA_Default),
                    Project = projectA,
                    Name = "default",
                    Environment = "default",
                    ResourceType = ResourceType.VirtualNetwork,
                    Subnets = new[]
                    {
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.Parse(NetworkA_Default_Subnet),
                            Name = "default"
                        },
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.Parse(NetworkA_Other_Subnet),
                            Name = "other"
                        }
                    }.ToList()
                },
                new VirtualNetwork
                {
                    Id = Guid.Parse(NetworkB_Default),
                    Project = projectB,
                    Name = "default",
                    Environment = "default",
                    ResourceType = ResourceType.VirtualNetwork,
                    Subnets = new[]
                    {
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.Parse(NetworkB_Default_Subnet),
                            Name = "default",
                            IpPools = new List<IpPool>(new []
                            {
                                new IpPool
                                {
                                    Id = Guid.NewGuid(),
                                    Name = "default",
                                },
                                new IpPool
                                {
                                    Id = Guid.NewGuid(),
                                    Name = "other",
                                }
                            })
                        },
                    }.ToList(),

                },
                new VirtualNetwork
                {
                    Id = Guid.Parse(NetworkB_Env2_Default),
                    Project = projectB,
                    Name = "default",
                    Environment = "env2",
                    ResourceType = ResourceType.VirtualNetwork,
                    Subnets = new[]
                    {
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.Parse(NetworkB_Env2_Subnet),
                            Name = "default"
                        },
                    }.ToList(),

                }
            };
        }
    }
}
