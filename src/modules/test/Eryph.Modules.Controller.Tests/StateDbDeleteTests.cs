using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;

namespace Eryph.Modules.Controller.Tests;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlStateDbTests(MySqlFixture databaseFixture)
    : StateDbDeleteTests(databaseFixture);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteStateDbTests(SqliteFixture databaseFixture)
    : StateDbDeleteTests(databaseFixture);

/// <summary>
/// This test verifies that deletes cascade as expected in the state database.
/// The default behavior can differ significantly depending on the used DBMS
/// and the inheritance strategy (TPH, TPC).
/// </summary>
public abstract class StateDbDeleteTests(IDatabaseFixture databaseFixture) : StateDbTestBase(databaseFixture)
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid VirtualNetworkId = Guid.NewGuid();
    private static readonly Guid VirtualSubnetId = Guid.NewGuid();
    private static readonly Guid VirtualIpPoolId = Guid.NewGuid();
    private static readonly Guid FloatingPortId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
    private static readonly Guid CatletPortId = Guid.NewGuid();
    private static readonly Guid IpAssignmentId = Guid.NewGuid();

    [Fact]
    public async Task VirtualNetwork_delete_cascades()
    {
        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();

            await stateStore.For<Project>().AddAsync(new Project()
            {
                Id = ProjectId,
                Name = "test-project",
                TenantId = EryphConstants.DefaultTenantId,
            });

            await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata()
            {
                Id = CatletMetadataId,
            });

            await stateStore.For<VirtualNetwork>().AddAsync(new VirtualNetwork()
            {
                Id = VirtualNetworkId,
                Name = "virtual-test-network",
                Environment = "test-environment",
                ProjectId = ProjectId,
                Subnets =
                [
                    new VirtualNetworkSubnet()
                {
                    Id = VirtualSubnetId,
                    Name = "virtual-test-subnet",
                    IpNetwork = "10.0.0.0/20",
                    MTU = 1400,
                    IpPools =
                    [
                        new IpPool()
                        {
                            Id = VirtualIpPoolId,
                            Name = "virtual-test-pool",
                            FirstIp = "10.0.0.100",
                            NextIp = "10.0.0.110",
                            LastIp = "10.0.0.200",
                        },
                    ],
                },
            ],
            });

            await stateStore.For<FloatingNetworkPort>().AddAsync(new FloatingNetworkPort()
            {
                Id = FloatingPortId,
                Name = "test-floating-port",
                MacAddress = "42:00:42:00:00:10",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
            });

            await stateStore.For<CatletNetworkPort>().AddAsync(new CatletNetworkPort()
            {
                Id = CatletPortId,
                Name = "test-catlet-port",
                CatletMetadataId = CatletMetadataId,
                NetworkId = VirtualNetworkId,
                FloatingPortId = FloatingPortId,
                MacAddress = "42:00:42:00:00:01",
                IpAssignments =
                [
                    new IpPoolAssignment()
                    {
                        Id = IpAssignmentId,
                        SubnetId = VirtualSubnetId,
                        PoolId = VirtualIpPoolId,
                        IpAddress = "10.0.0.104",
                        Number = 5,
                    },
                ],
            });
            await stateStore.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var virtualNetwork = await stateStore.For<VirtualNetwork>()
                .GetByIdAsync(VirtualNetworkId);
            await stateStore.For<VirtualNetwork>().DeleteAsync(virtualNetwork!);
            await stateStore.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var subnets = await stateStore.For<Subnet>().ListAsync();
            subnets.Should().BeEmpty();
            var ipPools = await stateStore.For<IpPool>().ListAsync();
            ipPools.Should().BeEmpty();
            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Provider_subnet_delete_cascades()
    {
        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var configRealizer = new NetworkProvidersConfigRealizer(stateStore);
            await configRealizer.RealizeConfigAsync(GetProvidersConfig(), default);
            await scope.GetInstance<IStateStore>().SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(
                new ProviderSubnetSpecs.GetForChangeTracking());

            await stateStore.For<FloatingNetworkPort>().AddAsync(new FloatingNetworkPort()
            {
                Id = FloatingPortId,
                Name = "test-floating-port",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
                MacAddress = "42:00:42:00:00:01",
                IpAssignments =
                [
                    new IpPoolAssignment()
                    {
                        Id = IpAssignmentId,
                        SubnetId = subnets[0].Id,
                        PoolId = subnets[0].IpPools[0].Id,
                        IpAddress = "10.0.0.104",
                        Number = 5,
                    },
                ],
            });
            await stateStore.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync();
            await stateStore.For<ProviderSubnet>().DeleteAsync(subnets[0]);
            await stateStore.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var stateStore = scope.GetInstance<IStateStore>();
            var subnets = await stateStore.For<Subnet>().ListAsync();
            subnets.Should().BeEmpty();
            var ipPools = await stateStore.For<IpPool>().ListAsync();
            ipPools.Should().BeEmpty();
            var ipAssignments = await stateStore.For<IpAssignment>().ListAsync();
            ipAssignments.Should().BeEmpty();
        }
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }

    private NetworkProvidersConfiguration GetProvidersConfig() => new()
    {
        NetworkProviders =
        [
            new NetworkProvider()
            {
                Name = "test-provider",
                TypeString = "nat_overlay",
                Subnets =
                [
                    new NetworkProviderSubnet()
                    {
                        Name = "provider-test-subnet",
                        Network = "10.0.0.0/24",
                        IpPools =
                        [
                            new NetworkProviderIpPool()
                            {
                                Name = "provider-test-pool",
                                FirstIp = "10.0.0.100",
                                NextIp = "10.0.0.100",
                                LastIp = "10.0.0.200",
                            }
                        ],
                    },
                ],
            },
        ],
    };
}
