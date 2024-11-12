using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Tests.Networks;

public sealed class CatletIpManagerTests : InMemoryStateDbTestBase
{
    private const string DefaultProjectId = "4B4A3FCF-B5ED-4A9A-AB6E-03852752095E";
    private const string SecondProjectId = "75c27daf-77c8-4b98-a072-a4706dceb422";

    private const string DefaultNetworkId = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
    private const string DefaultSubnetId = "ed6697cd-836f-4da7-914b-b09ed1567934";
    private const string SecondSubnetId = "4f976208-613a-40d4-a284-d32cbd4a1b8e";

    private const string SecondNetworkId = "e480a020-57d0-4443-a973-57aa0c95872e";
    private const string SecondNetworkSubnetId = "27ec11a4-5d6a-47da-9f9f-eb7486db38ea";

    private const string SecondEnvironmentNetworkId = "81a139e5-ab61-4fe3-b81f-59c11a665d22";
    private const string SecondEnvironmentSubnetId = "dc807357-50e7-4263-8298-0c97ff69f4cf";

    private const string SecondProjectNetworkId = "c0043e88-8268-4ac0-b027-2fa37ad3168f";
    private const string SecondProjectSubnetId = "0c721846-5e2e-40a9-83d2-f1b75206ef84";

    private const string CatletMetadataId = "15e2b061-c625-4469-9fe7-7c455058fcc0";
    private static readonly Guid CatletPortId = Guid.NewGuid();

    [Theory]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, null, null, null, "10.0.0.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, "default", "default", "default", "10.0.0.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "default", SecondNetworkId, "second-network", "default", "default", "10.5.0.12")]
    [InlineData(DefaultProjectId, "second-environment", DefaultNetworkId, "default", "default", "default", "10.10.0.12")]
    // When the environment does not have a dedicated network, we should fall back to the default network
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, null, null, null, "10.0.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, "default", "default", "default", "10.0.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", SecondNetworkId, "second-network", "default", "default", "10.5.0.12")]
    [InlineData(SecondProjectId, "default", SecondProjectNetworkId, null, null, null, "10.100.0.12")]
    [InlineData(SecondProjectId, "default", SecondProjectNetworkId, "default", "default", "default", "10.100.0.12")]
    public async Task ConfigurePortIps_NewPortIsAdded_AssignmentIsCreated(
        string projectId,
        string environment,
        string networkId, 
        string? networkName,
        string? subnetName,
        string? poolName,
        string expectedIpAddress)
    {
        var networkConfig = new CatletNetworkConfig()
        {
            Name = networkName,
            SubnetV4 = subnetName != null || poolName != null
                ? new CatletSubnetConfig
                {
                    Name = subnetName,
                    IpPool = poolName
                }
                : null,
        };

        await WithScope(async (ipManager, _, stateStore) =>
        {
            var catletPort = new CatletNetworkPort
            {
                Id = CatletPortId,
                Name = "test-catlet-port",
                MacAddress = "00:00:00:00:00:01",
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

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, _, stateStore) =>
        {
            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().SatisfyRespectively(
                ipAssignment =>
                {
                    ipAssignment.NetworkPortId.Should().Be(CatletPortId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);
                });
        });
    }

    [Theory]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, DefaultSubnetId, null, null, null, "10.0.0.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, DefaultSubnetId, "default", "default", "default", "10.0.0.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, DefaultSubnetId, "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "default", DefaultNetworkId, SecondSubnetId, "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "default", SecondNetworkId, SecondNetworkSubnetId, "second-network", "default", "default", "10.5.0.12")]
    [InlineData(DefaultProjectId, "second-environment", SecondEnvironmentNetworkId, SecondEnvironmentSubnetId, "default", "default", "default", "10.10.0.12")]
    // When the environment does not have a dedicated network, we should fall back to the default network
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, DefaultSubnetId, null, null, null, "10.0.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, DefaultSubnetId, "default", "default", "default", "10.0.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, DefaultSubnetId, "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "environment-without-network", DefaultNetworkId, SecondSubnetId, "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", SecondNetworkId, SecondNetworkSubnetId, "second-network", "default", "default", "10.5.0.12")]
    [InlineData(SecondProjectId, "default", SecondProjectNetworkId, SecondProjectSubnetId, null, null, null, "10.100.0.12")]
    [InlineData(SecondProjectId, "default", SecondProjectNetworkId, SecondProjectSubnetId, "default", "default", "default", "10.100.0.12")]
    public async Task ConfigureFloatingPortIps_AssignmentIsValid_AssignmentIsNotChanged(
        string projectId,
        string environment,
        string networkId,
        string subnetId,
        string? networkName,
        string? subnetName,
        string? poolName,
        string expectedIpAddress)
    {
        var ipAssignmentId = Guid.Empty;
        await WithScope(async (_, ipPoolManager, stateStore) =>
        {
            var ipAssignmentResult = ipPoolManager.AcquireIp(
                Guid.Parse(subnetId), poolName ?? EryphConstants.DefaultIpPoolName);
            var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
            ipAssignmentId = ipAssignment.Id;

            var catletPort = new CatletNetworkPort
            {
                Id = CatletPortId,
                Name = "test-catlet-port",
                MacAddress = "00:00:00:00:00:01",
                NetworkId = Guid.Parse(networkId),
                CatletMetadataId = Guid.Parse(CatletMetadataId),
                IpAssignments = [ipAssignment],
            };

            await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
            await stateStore.SaveChangesAsync();
        });

        var networkConfig = new CatletNetworkConfig()
        {
            Name = networkName,
            SubnetV4 = subnetName != null || poolName != null
                ? new CatletSubnetConfig
                {
                    Name = subnetName,
                    IpPool = poolName
                }
                : null,
        };

        await WithScope(async (catletIpManager, _, stateStore) =>
        {
            var catletPort = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(CatletPortId);
            catletPort.Should().NotBeNull();

            var result = await catletIpManager.ConfigurePortIps(
                Guid.Parse(projectId), environment, catletPort!, networkConfig,
                CancellationToken.None);

            result.Should().BeRight().Which.Should().SatisfyRespectively(
                ipAddress => ipAddress.ToString().Should().Be(expectedIpAddress));

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, _, stateStore) =>
        {
            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().SatisfyRespectively(
                ipAssignment =>
                {
                    ipAssignment.NetworkPortId.Should().Be(CatletPortId);
                    ipAssignment.Id.Should().Be(ipAssignmentId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);
                });
        });
    }

    [Theory]
    [InlineData(DefaultProjectId, "default", "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "default", "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "default", "second-network", "default", "default", "10.5.0.12")]
    [InlineData(DefaultProjectId, "second-environment", "default", "default", "default", "10.10.0.12")]
    // When the environment does not have a dedicated network, we should fall back to the default network
    [InlineData(DefaultProjectId, "environment-without-network", "default", "default", "second-pool", "10.0.1.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "default", "second-subnet", "default", "10.1.0.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "second-network", "default", "default", "10.5.0.12")]
    [InlineData(SecondProjectId, "default", null, null, null, "10.100.0.12")]
    [InlineData(SecondProjectId, "default", "default", "default", "default", "10.100.0.12")]
    public async Task ConfigurePortIps_AssignmentIsInvalid_AssignmentIsChanged(
        string projectId,
        string environment,
        string? networkName,
        string? subnetName,
        string? poolName,
        string expectedIpAddress)
    {
        var ipAssignmentId = Guid.Empty;
        await WithScope(async (_, ipPoolManager, stateStore) =>
        {
            var ipAssignmentResult = ipPoolManager.AcquireIp(
                Guid.Parse(DefaultSubnetId),
                EryphConstants.DefaultIpPoolName);
            var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
            ipAssignmentId = ipAssignment.Id;

            var catletPort = new CatletNetworkPort
            {
                Id = CatletPortId,
                Name = "test-catlet-port",
                MacAddress = "00:00:00:00:00:01",
                NetworkId = Guid.Parse(DefaultNetworkId),
                CatletMetadataId = Guid.Parse(CatletMetadataId),
                IpAssignments = [ipAssignment],
            };

            await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
            await stateStore.SaveChangesAsync();
        });

        var networkConfig = new CatletNetworkConfig()
        {
            Name = networkName,
            SubnetV4 = subnetName != null || poolName != null
                ? new CatletSubnetConfig
                {
                    Name = subnetName,
                    IpPool = poolName
                }
                : null,
        };

        await WithScope(async (catletIpManager, _, stateStore) =>
        {
            var catletPort = await stateStore.Read<CatletNetworkPort>()
                .GetByIdAsync(CatletPortId);
            catletPort.Should().NotBeNull();

            var result = await catletIpManager.ConfigurePortIps(
                Guid.Parse(projectId), environment, catletPort!, networkConfig,
                CancellationToken.None);

            result.Should().BeRight().Which.Should().SatisfyRespectively(
                ipAddress => ipAddress.ToString().Should().Be(expectedIpAddress));

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, _, stateStore) =>
        {
            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().SatisfyRespectively(
                ipAssignment =>
                {
                    ipAssignment.NetworkPortId.Should().Be(CatletPortId);
                    ipAssignment.Id.Should().NotBe(ipAssignmentId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);
                });
        });
    }
        
    // TODO Add negative test

    private async Task WithScope(Func<ICatletIpManager, IIpPoolManager, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var catletIpManager = scope.GetInstance<ICatletIpManager>();
        var ipPoolManager = scope.GetInstance<IIpPoolManager>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(catletIpManager, ipPoolManager, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        // Use the proper IpPoolManager instead of a mock as the code quite
        // interdependent as it modifies the same EF Core entities.
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

        var projectB = new Project()
        {
            Id = Guid.Parse(SecondProjectId),
            Name = "second-project",
            TenantId = EryphConstants.DefaultTenantId,
        };
        await stateStore.For<Project>().AddAsync(projectB);

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(DefaultNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(DefaultSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.0.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.0.0.0/16",
                                FirstIp = "10.0.0.10",
                                NextIp = "10.0.0.12",
                                LastIp = "10.0.0.19",
                            },
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = "second-pool",
                                IpNetwork = "10.0.0.0/16",
                                FirstIp = "10.0.1.10",
                                NextIp = "10.0.1.12",
                                LastIp = "10.0.1.19",
                            }
                        ],
                    },
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondSubnetId),
                        Name = "second-subnet",
                        IpNetwork = "10.1.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.1.0.0/16",
                                FirstIp = "10.1.0.10",
                                NextIp = "10.1.0.12",
                                LastIp = "10.1.0.19",
                            }
                        ],
                    },
                ],
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = "second-network",
                Environment = EryphConstants.DefaultEnvironmentName,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondNetworkSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.5.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.5.0.0/16",
                                FirstIp = "10.5.0.10",
                                NextIp = "10.5.0.12",
                                LastIp = "10.5.0.19",
                            }
                        ],
                    },
                ],
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondEnvironmentNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultNetworkName,
                Environment = "second-environment",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondEnvironmentSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.10.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.10.0.0/16",
                                FirstIp = "10.10.0.10",
                                NextIp = "10.10.0.12",
                                LastIp = "10.10.0.19",
                            }
                        ],
                    },
                ],
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondProjectNetworkId),
                ProjectId = Guid.Parse(SecondProjectId),
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondProjectSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.100.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.100.0.0/16",
                                FirstIp = "10.100.0.10",
                                NextIp = "10.100.0.12",
                                LastIp = "10.100.0.19",
                            }
                        ],
                    },
                ],
            });
    }
}