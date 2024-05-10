using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;

namespace Eryph.StateDb.Tests;

public class MySqlStateDbTests() : StateDbTests(DatabaseType.MySql);

public class SqliteStateDbTests() : StateDbTests(DatabaseType.Sqlite);

public class SqlServerStateDbTests() : StateDbTests(DatabaseType.SqlServer);

public abstract class StateDbTests(DatabaseType databaseType) : StateDbTestBase(databaseType)
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
                MacAddress = "00:00:00:00:00:01",
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

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }
}
