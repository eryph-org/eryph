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
        private const string ProjectAId = "96bbd6d7-01f9-4001-8c86-3fba75baa1b5";
        private const string ProjectA_NetworkId = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
        private const string NetworkA_Default_Subnet = "ed6697cd-836f-4da7-914b-b09ed1567934";
        private const string NetworkA_Other_Subnet = "29fb8b37-4779-427a-bc5c-9a5eccffd5e2";

        private const string ProjectBId = "75c27daf-77c8-4b98-a072-a4706dceb422";
        private const string NetworkB_Default = "a0ce4b1a-03e6-413b-a048-567079b49b28";
        private const string NetworkB_Env2_Default = "29408ed0-f876-4879-9faa-deb519d1df0a";
        private const string NetworkB_Default_Subnet = "ac451fa5-3364-4593-aa4d-14f95529fd54";
        private const string NetworkB_Env2_Subnet = "91a25d95-f417-482d-9264-e4179f61e379";

        private const string CatletMetadataId = "15e2b061-c625-4469-9fe7-7c455058fcc0";
        // ReSharper restore InconsistentNaming


        private readonly Mock<IIpPoolManager> _ipPoolManagerMock = new();

        private readonly SqliteConnection _connection;

        public CatletIpManagerTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
        }

        
        [Theory]
        [InlineData(ProjectAId, ProjectA_NetworkId, "default", null, null, null, "192.0.2.1")]
        [InlineData(ProjectAId, ProjectA_NetworkId, "default", "default", "other", null, "192.0.2.17")]
        [InlineData(ProjectAId, ProjectA_NetworkId, "env2", null, null, null, "192.0.2.1")]
        [InlineData(ProjectBId, NetworkB_Default, "default", null, null, null, "192.0.2.33")]
        [InlineData(ProjectBId, NetworkB_Default, "default", "default", "default", "other", "192.0.2.49")]
        [InlineData(ProjectBId, NetworkB_Env2_Default, "env2", null, null, null, "192.0.2.65")]
        public async Task Adds_catlet_network_port_to_expected_pool(
            string projectId,
            string networkId, 
            string environment,
            string? network,
            string? subnet,
            string? pool,
            string expectedIpAddress)
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

            await WithScope(async (ipManager, _, stateStore) =>
            {
                var catletPort = new CatletNetworkPort
                {
                    Id = Guid.NewGuid(),
                    Name = "test-catlet-port",
                    NetworkId = Guid.Parse(networkId),
                    CatletMetadataId = Guid.Parse(CatletMetadataId),
                };
                await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);

                var result = await ipManager.ConfigurePortIps(
                    Guid.Parse(projectId),
                    environment, catletPort, networkConfig,
                    CancellationToken.None);

                result.Should().BeRight().Which.Should().SatisfyRespectively(
                    ipAddress => ipAddress.ToString().Should().Be(expectedIpAddress));
            });

            _ipPoolManagerMock.Verify();
        }

        // TODO Parameterize for different subnets
        [Fact]
        public async Task Deletes_Invalid_Port()
        {
            var projectId = Guid.Parse(ProjectAId);
            var networkConfig = new CatletNetworkConfig()
            {
                Name = "default"
            };

            var catletPortId = Guid.NewGuid();
            var ipAssignmentId = Guid.Empty;
            await WithScope(async (_, ipPoolManager, stateStore) =>
            {
                var ipAssignmentResult = ipPoolManager.AcquireIp(
                    Guid.Parse(NetworkA_Other_Subnet),
                    EryphConstants.DefaultIpPoolName);
                var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
                ipAssignmentId = ipAssignment.Id;

                var catletPort = new CatletNetworkPort
                {
                    Id = catletPortId,
                    Name = "test-catlet-port",
                    NetworkId = Guid.Parse(ProjectA_NetworkId),
                    CatletMetadataId = Guid.Parse(CatletMetadataId),
                    IpAssignments = [ipAssignment],
                };
                
                await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
                await stateStore.SaveChangesAsync();
            });

            await WithScope(async (catletIpManager, _, stateStore) =>
            {
                var catletPort = await stateStore.Read<CatletNetworkPort>()
                    .GetByIdAsync(catletPortId);
                catletPort.Should().NotBeNull();
                await stateStore.LoadCollectionAsync(catletPort!, p => p.IpAssignments);
                catletPort!.IpAssignments.Should().SatisfyRespectively(
                    ipAssignment => ipAssignment.Id.Should().Be(ipAssignmentId));

                var result = await catletIpManager.ConfigurePortIps(projectId,
                    "default", catletPort, networkConfig,
                    CancellationToken.None);

                result.Should().BeRight().Which.Should().SatisfyRespectively(
                    ipAddress => ipAddress.ToString().Should().Be("192.0.2.1"));

                await stateStore.SaveChangesAsync();
            });

            await WithScope(async (_, _, stateStore) =>
            {
                var catletPort = await stateStore.Read<CatletNetworkPort>()
                    .GetByIdAsync(catletPortId);
                catletPort.Should().NotBeNull();
                await stateStore.LoadCollectionAsync(catletPort!, p => p.IpAssignments);

                catletPort!.IpAssignments.Should().SatisfyRespectively(
                    ipAssignment => ipAssignment.IpAddress.Should().Be("192.0.2.1"));
            });
        }

        [Fact]
        public async Task ExistingAssignmentInDifferentEnvironment_RemovesOldAssignment()
        {
            
        }

        [Fact]
        public async Task Keeps_Valid_Port()
        {
            var projectId = Guid.Parse(ProjectAId);
            var networkConfig = new CatletNetworkConfig()
            {
                Name = "default"
            };

            var catletPortId = Guid.NewGuid();
            var ipAssignmentId = Guid.Empty;
            await WithScope(async (_, ipPoolManager, stateStore) =>
            {
                var ipAssignmentResult = ipPoolManager.AcquireIp(
                    Guid.Parse(NetworkA_Default_Subnet),
                    EryphConstants.DefaultIpPoolName);
                var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
                ipAssignmentId = ipAssignment.Id;

                var catletPort = new CatletNetworkPort
                {
                    Id = catletPortId,
                    Name = "test-catlet-port",
                    NetworkId = Guid.Parse(ProjectA_NetworkId),
                    CatletMetadataId = Guid.Parse(CatletMetadataId),
                    IpAssignments = [ipAssignment]
                };

                await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
                await stateStore.SaveChangesAsync();
            });

            await WithScope(async (catletIpManager, _, stateStore) =>
            {
                var catletPort = await stateStore.For<CatletNetworkPort>()
                    .GetByIdAsync(catletPortId);
                await stateStore.LoadCollectionAsync(catletPort!, p => p.IpAssignments);
                catletPort!.IpAssignments.Should().SatisfyRespectively(
                    ipAssignment => ipAssignment.Id.Should().Be(ipAssignmentId));

                var result = await catletIpManager.ConfigurePortIps(projectId,
                    "default", catletPort, networkConfig,
                    CancellationToken.None);

                result.Should().BeRight().Which.Should().SatisfyRespectively(
                    ipAddress => ipAddress.ToString().Should().Be("192.0.2.1"));
                
                await stateStore.SaveChangesAsync();
            });

            await WithScope(async (_, _, stateStore) =>
            {
                var catletPort = await stateStore.Read<CatletNetworkPort>()
                    .GetByIdAsync(catletPortId);
                catletPort.Should().NotBeNull();
                await stateStore.LoadCollectionAsync(catletPort!, p => p.IpAssignments);

                catletPort!.IpAssignments.Should().SatisfyRespectively(
                    ipAssignment => ipAssignment.Id.Should().Be(ipAssignmentId));
            });
        }

        private async Task WithScope(Func<ICatletIpManager, IIpPoolManager, IStateStore, Task> func)
        {
            await using var scope = CreateScope();
            var catletIpManager = scope.GetInstance<ICatletIpManager>();
            var ipPoolManager = scope.GetInstance<IIpPoolManager>();
            var stateStore = scope.GetInstance<IStateStore>();
            await func(catletIpManager, ipPoolManager, stateStore);
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
            options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
            options.Container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
        }

        protected override async Task SeedAsync(IStateStore stateStore)
        {
            await SeedDefaultTenantAndProject();

            await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
            {
                Id = Guid.Parse(CatletMetadataId),
            });

            var projectA = new Project()
            {
                Id = Guid.Parse(ProjectAId),
                Name = "project-a",
                TenantId = EryphConstants.DefaultTenantId,
            };
            await stateStore.For<Project>().AddAsync(projectA);

            var projectB = new Project()
            {
                Id = Guid.Parse(ProjectBId),
                Name = "project-b",
                TenantId = EryphConstants.DefaultTenantId,
            };
            await stateStore.For<Project>().AddAsync(projectB);

            var projectADefaultEnvNetwork = new VirtualNetwork
            {
                Id = Guid.Parse(ProjectA_NetworkId),
                Project = projectA,
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                ResourceType = ResourceType.VirtualNetwork,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkA_Default_Subnet),
                        Name = EryphConstants.DefaultSubnetName,
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "192.0.2.0/28",
                                FirstIp = "192.0.2.1",
                                NextIp = "192.0.2.1",
                                LastIp = "192.0.2.11",
                            }
                        ],
                    },
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(NetworkA_Other_Subnet),
                        Name = "other",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "192.0.2.16/28",
                                FirstIp = "192.0.2.17",
                                NextIp = "192.0.2.17",
                                LastIp = "192.0.2.27",
                            }
                        ],
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
                                IpNetwork = "192.0.2.32/28",
                                FirstIp = "192.0.2.33",
                                NextIp = "192.0.2.33",
                                LastIp = "192.0.2.43",
                            },
                            new IpPool
                            {
                                Id = Guid.NewGuid(),
                                Name = "other",
                                IpNetwork = "192.0.2.48/28",
                                FirstIp = "192.0.2.49",
                                NextIp = "192.0.2.49",
                                LastIp = "192.0.2.59",
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
                        Name = "default",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "192.0.2.64/28",
                                FirstIp = "192.0.2.65",
                                NextIp = "192.0.2.65",
                                LastIp = "192.0.2.75",
                            }
                        ],
                    },
                ],
            };
            await stateStore.For<VirtualNetwork>().AddAsync(projectBOtherEnvNetwork);
        }
    }
}
