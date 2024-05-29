using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Tests.Networks;

public class IpPoolManagerTests : InMemoryStateDbTestBase
{
    private static readonly Guid NetworkId = Guid.NewGuid();
    private static readonly Guid SubnetId = Guid.NewGuid();
    private static readonly Guid IpPoolId = Guid.NewGuid();
    private const string IpPoolName = "test-pool";
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
    private static readonly Guid NetworkPortId = Guid.NewGuid();

    [Fact]
    public async Task AcquireIp_NextIpAvailable_ReturnsNextIp()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.100");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.NextIp.Should().Be("10.0.0.101");
        });
    }

    [Fact]
    public async Task AcquireIp_NextIpNotAvailable_ReturnsNextFreeIp()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            await AcquireIpAndAssign(poolManager);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            pool!.NextIp = "10.0.0.100";
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.101");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.NextIp.Should().Be("10.0.0.102");
        });
    }

    [Fact]
    public async Task AcquireIp_HighestIpIsUsed_ReturnsFirstFreeIpAfterRollover()
    {
        Guid firstAssignmentId = default;
        Guid secondAssignmentId = default;
        await WithScope(async (poolManager, stateStore) =>
        {
            firstAssignmentId = (await AcquireIpAndAssign(poolManager)).Id;
            secondAssignmentId = (await AcquireIpAndAssign(poolManager)).Id;
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var firstAssignment = await stateStore.For<IpAssignment>().GetByIdAsync(firstAssignmentId);
            var secondAssignment = await stateStore.For<IpAssignment>().GetByIdAsync(secondAssignmentId);
            await stateStore.For<IpAssignment>().DeleteRangeAsync(
                [firstAssignment!, secondAssignment!]);

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.100");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.NextIp.Should().Be("10.0.0.101");
        });
    }

    // This test also ensures that the logic terminates when all IP addresses
    // have been allocated after a rollover.
    [Fact]
    public async Task AcquireIp_HighestIpIsUsedAndOneIpLeft_ReturnsFreeIpAfterRollover()
    {
        Guid assignmentId = default;
        await WithScope(async (poolManager, stateStore) =>
        {
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);
            assignmentId = (await AcquireIpAndAssign(poolManager)).Id; 
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var assignment = await stateStore.For<IpAssignment>().GetByIdAsync(assignmentId);
            await stateStore.For<IpAssignment>().DeleteAsync(assignment!);

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.102");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.NextIp.Should().Be("10.0.0.100");
        });
    }

    // This test also ensures that the logic terminates when all IP addresses
    // have been allocated.
    [Fact]
    public async Task AcquireIp_AllIpAddressesInUse_ReturnsError()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);
            await AcquireIpAndAssign(poolManager);

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var error = result.Should().BeLeft().Subject;
            error.Message.Should().Be($"IP pool {IpPoolName}({IpPoolId}) has no more free IP addresses.");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.NextIp.Should().Be($"10.0.0.100");
        });
    }

    private async Task WithScope(Func<IIpPoolManager, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var poolManager = scope.GetInstance<IIpPoolManager>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(poolManager, stateStore);
    }


    private async Task<IpPoolAssignment> AcquireIpAndAssign(IIpPoolManager ipPoolManager)
    {
        var result = await ipPoolManager.AcquireIp(SubnetId, IpPoolName);
        var ipAssignment = result.Should().BeRight().Subject;
        ipAssignment.NetworkPortId = NetworkPortId;
        return ipAssignment;
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork()
            {
                Id = NetworkId,
                Name = "test-network",
                ProjectId = EryphConstants.DefaultProjectId,
                Subnets =
                [
                    new VirtualNetworkSubnet()
                    {
                        Id = SubnetId,
                        Name = "test-subnet",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = IpPoolId,
                                Name = IpPoolName,
                                IpNetwork = "10.0.0.0/24",
                                FirstIp = "10.0.0.100",
                                NextIp = "10.0.0.100",
                                LastIp = "10.0.0.104",
                            }
                        ]
                    },
                ]
            });

        await stateStore.For<CatletMetadata>().AddAsync(
            new CatletMetadata()
            {
                Id = CatletMetadataId,
            });
        
        await stateStore.For<CatletNetworkPort>().AddAsync(
            new CatletNetworkPort()
            {
                Id = NetworkPortId,
                Name = "test-catlet-port",
                CatletMetadataId = CatletMetadataId,
                NetworkId = NetworkId,
            });
    }
}
