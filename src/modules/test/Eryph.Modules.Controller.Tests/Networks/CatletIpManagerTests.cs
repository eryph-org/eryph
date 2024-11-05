using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Modules.Controller.Networks;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Sqlite;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Xunit;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public sealed class CatletIpManagerTests : InMemoryStateDbTestBase
    {
        // ReSharper disable InconsistentNaming
        private const string ProjectA = "96bbd6d7-01f9-4001-8c86-3fba75baa1b5";
        private const string NetworkA_Default = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
        private const string NetworkA_Default_Subnet = "ed6697cd-836f-4da7-914b-b09ed1567934";
        private const string NetworkA_Other_Subnet = "29fb8b37-4779-427a-bc5c-9a5eccffd5e2";

        private const string ProjectB = "75c27daf-77c8-4b98-a072-a4706dceb422";
        private const string NetworkB_Default = "a0ce4b1a-03e6-413b-a048-567079b49b28";
        private const string NetworkB_Env2_Default = "29408ed0-f876-4879-9faa-deb519d1df0a";
        private const string NetworkB_Default_Subnet = "ac451fa5-3364-4593-aa4d-14f95529fd54";
        private const string NetworkB_Env2_Subnet = "91a25d95-f417-482d-9264-e4179f61e379";

        private const string CatletMetadata = "15e2b061-c625-4469-9fe7-7c455058fcc0";
        // ReSharper restore InconsistentNaming


        private readonly Mock<IIpPoolManager> _ipPoolManagerMock = new();

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
            var networkConfig = new CatletNetworkConfig()
            {
                Name = network,
                SubnetV4 = subnet != null || pool != null
                    ? new CatletSubnetConfig
                    {
                        Name = subnet ?? "default",
                        IpPool = pool
                    }
                    : null,
            };

            var catletPort = new CatletNetworkPort
            {
                Id = Guid.NewGuid(),
                Name = "test-catlet-port",
                CatletMetadataId = Guid.Parse(CatletMetadata),
            };
            var projectId = Guid.Parse(projectIdString);
            var subnetId = Guid.Parse(subnetIdString);

            ArrangeAcquireIp(subnetId, pool ?? "default", "192.168.2.1");

            await WithScope(async (ipManager, _) =>
            {
                var result = await ipManager.ConfigurePortIps(projectId,
                    environment, catletPort, networkConfig,
                    CancellationToken.None);

                result.Should().BeRight().Which.Should().SatisfyRespectively(
                    ipAddress => ipAddress.ToString().Should().Be("192.168.2.1"));
            });

            _ipPoolManagerMock.Verify();
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
                Name = "test-catlet-port",
                NetworkId = Guid.Parse(NetworkA_Default_Subnet),
                CatletMetadataId = Guid.Parse(CatletMetadata),
                IpAssignments =
                [
                    new IpPoolAssignment()
                    {
                        SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                        Pool = new IpPool
                        {
                            SubnetId = Guid.Parse(NetworkA_Default_Subnet),
                            Name = "other"
                        },
                    }
                ]
            };

            var contextOptions = new DbContextOptionsBuilder<SqliteStateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new SqliteStateStoreContext(contextOptions);
            var ipManager = await SetupPortTest(context, catletPort);
            var stateStore = new StateStore(context);
            var port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);
            port!.IpAssignments.Should().HaveCount(1);

            var result = await ipManager.ConfigurePortIps(projectId,
                "default", catletPort, networkConfig,
                CancellationToken.None);

            result.Should().BeRight();

            await context.SaveChangesAsync();
            port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);

            port!.IpAssignments.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task ExistingAssignmentInDifferentEnvironment_RemovesOldAssignment()
        {
            
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
                Name = "test-catlet-port",
                NetworkId = Guid.Parse(NetworkA_Default_Subnet),
                CatletMetadataId = Guid.Parse(CatletMetadata),
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

            var contextOptions = new DbContextOptionsBuilder<SqliteStateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new SqliteStateStoreContext(contextOptions);
            var ipManager = await SetupPortTest(context, catletPort);
            var stateStore = new StateStore(context);
            var port = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(catletPort.Id, CancellationToken.None);
            port!.IpAssignments.Should().HaveCount(1);

            var result = await ipManager.ConfigurePortIps(projectId,
                "default", catletPort, networkConfig,
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

            await stateStore.For<CatletMetadata>().AddAsync(new()
            {
                Id = Guid.Parse(CatletMetadata),
            });

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

        private async Task WithScope(Func<ICatletIpManager, IStateStore, Task> func)
        {
            await using var scope = CreateScope();
            var catletIpManager = scope.GetInstance<ICatletIpManager>();
            var stateStore = scope.GetInstance<IStateStore>();
            await func(catletIpManager, stateStore);
        }

        private void ArrangeAcquireIp(Guid subnetId, string poolName, string ipAddress)
        {
            _ipPoolManagerMock
                .Setup(x => x.AcquireIp(subnetId, poolName, It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, IpPoolAssignment>(new IpPoolAssignment
                {
                    IpAddress = ipAddress
                }))
                .Verifiable();
        }

        protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.Container.RegisterInstance(_ipPoolManagerMock.Object);
            options.Container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
        }

        protected override async Task SeedAsync(IStateStore stateStore)
        {
            await SeedDefaultTenantAndProject();

            var projectA = new Project()
            {
                Id = Guid.Parse(ProjectA),
                Name = "project-a",
                TenantId = EryphConstants.DefaultTenantId,
            };
            await stateStore.For<Project>().AddAsync(projectA);

            var projectB = new Project()
            {
                Id = Guid.Parse(ProjectB),
                Name = "project-b",
                TenantId = EryphConstants.DefaultTenantId,
            };
            await stateStore.For<Project>().AddAsync(projectB);

            var projectADefaultEnvNetwork = new VirtualNetwork
            {
                Id = Guid.Parse(NetworkA_Default),
                Project = projectA,
                Name = "default",
                Environment = "default",
                ResourceType = ResourceType.VirtualNetwork,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkA_Default_Subnet),
                        Name = "default"
                    },
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkA_Other_Subnet),
                        Name = "other"
                    },
                ],
            };
            await stateStore.For<VirtualNetwork>().AddAsync(projectADefaultEnvNetwork);

            var projectBDefaultEnvNetwork = new VirtualNetwork
            {
                Id = Guid.Parse(NetworkB_Default),
                Project = projectB,
                Name = "default",
                Environment = "default",
                ResourceType = ResourceType.VirtualNetwork,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkB_Default_Subnet),
                        Name = "default",
                        IpPools =
                        [
                            new IpPool
                            {
                                Id = Guid.NewGuid(),
                                Name = "default",
                            },
                            new IpPool
                            {
                                Id = Guid.NewGuid(),
                                Name = "other",
                            },
                        ],
                    },
                ],
            };
            await stateStore.For<VirtualNetwork>().AddAsync(projectBDefaultEnvNetwork);
            
            var projectBOtherEnvNetwork = new VirtualNetwork
            {
                Id = Guid.Parse(NetworkB_Env2_Default),
                Project = projectB,
                Name = "default",
                Environment = "env2",
                ResourceType = ResourceType.VirtualNetwork,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkB_Env2_Subnet),
                        Name = "default"
                    },
                ],
            };
            await stateStore.For<VirtualNetwork>().AddAsync(projectBOtherEnvNetwork);
        }
    }
}
