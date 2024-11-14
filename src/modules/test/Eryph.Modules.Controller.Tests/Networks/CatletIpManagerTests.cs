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
    private const string DefaultNetworkId = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
    private const string DefaultSubnetId = "ed6697cd-836f-4da7-914b-b09ed1567934";
    private const string SecondSubnetId = "4f976208-613a-40d4-a284-d32cbd4a1b8e";

    private const string CatletMetadataId = "15e2b061-c625-4469-9fe7-7c455058fcc0";
    private static readonly Guid CatletPortId = Guid.NewGuid();

    [Theory]
    [InlineData(null, null, "10.0.0.12")]
    [InlineData("default", "default", "10.0.0.12")]
    [InlineData("default", "second-pool", "10.0.1.12")]
    [InlineData("second-subnet", "default", "10.1.0.12")]
    public async Task ConfigurePortIps_NewPortIsAdded_AssignmentIsCreated(
        string? subnetName,
        string? poolName,
        string expectedIpAddress)
    {
        var networkConfig = new CatletNetworkConfig()
        {
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
            var network = await stateStore.For<VirtualNetwork>()
                .GetByIdAsync(Guid.Parse(DefaultNetworkId));
            network.Should().NotBeNull();

            var catletPort = new CatletNetworkPort
            {
                Id = CatletPortId,
                Name = "test-catlet-port",
                MacAddress = "42:00:42:00:00:01",
                Network = network!,
                CatletMetadataId = Guid.Parse(CatletMetadataId),
            };
            await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);

            var result = await ipManager.ConfigurePortIps(
                network!, catletPort, networkConfig);

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
    [InlineData(DefaultSubnetId, null, null, "10.0.0.12")]
    [InlineData(DefaultSubnetId, "default", "default", "10.0.0.12")]
    [InlineData(DefaultSubnetId, "default", "second-pool", "10.0.1.12")]
    [InlineData(SecondSubnetId, "second-subnet", "default", "10.1.0.12")]
    public async Task ConfigurePortIps_AssignmentIsValid_AssignmentIsNotChanged(
        string subnetId,
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
                MacAddress = "42:00:42:00:00:01",
                NetworkId = Guid.Parse(DefaultNetworkId),
                CatletMetadataId = Guid.Parse(CatletMetadataId),
                IpAssignments = [ipAssignment],
            };

            await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
            await stateStore.SaveChangesAsync();
        });

        var networkConfig = new CatletNetworkConfig()
        {
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
            var catletPort = await stateStore.For<CatletNetworkPort>()
                .GetByIdAsync(CatletPortId);
            catletPort.Should().NotBeNull();
            var network = await stateStore.For<VirtualNetwork>()
                .GetByIdAsync(Guid.Parse(DefaultNetworkId));
            network.Should().NotBeNull();

            var result = await catletIpManager.ConfigurePortIps(
                network!, catletPort!, networkConfig);

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
    [InlineData("default", "second-pool", "10.0.1.12")]
    [InlineData("second-subnet", "default", "10.1.0.12")]
    public async Task ConfigurePortIps_AssignmentIsInvalid_AssignmentIsChanged(
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
                MacAddress = "42:00:42:00:00:01",
                NetworkId = Guid.Parse(DefaultNetworkId),
                CatletMetadataId = Guid.Parse(CatletMetadataId),
                IpAssignments = [ipAssignment],
            };

            await stateStore.For<CatletNetworkPort>().AddAsync(catletPort);
            await stateStore.SaveChangesAsync();
        });

        var networkConfig = new CatletNetworkConfig()
        {
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

            var network = await stateStore.Read<VirtualNetwork>()
                .GetByIdAsync(Guid.Parse(DefaultNetworkId));
            network.Should().NotBeNull();

            var result = await catletIpManager.ConfigurePortIps(
                network!, catletPort!, networkConfig);

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
    }
}
