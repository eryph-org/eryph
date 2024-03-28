using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Tests.Networks;

public class IpPoolManagerTests : StateDbTestBase
{
    private static readonly Guid NetworkId = Guid.NewGuid();
    private static readonly Guid SubnetId = Guid.NewGuid();
    private static readonly Guid IpPoolId = Guid.NewGuid();
    private const string IpPoolName = "test-pool";

    [Fact]
    public async Task AcquireIp_NextIpAvailable_ReturnsNextIp()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.100");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.Counter.Should().Be(1);
        });
    }

    [Fact]
    public async Task AcquireIp_NextIpNotAvailable_ReturnsNextFreeIp()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            poolManager.AcquireIp(SubnetId, IpPoolName).Should().BeRight();
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            pool!.Counter = 0;
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.101");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.Counter.Should().Be(2);
        });
    }

    [Fact]
    public async Task AcquireIp_LastIpIsUsed_ReturnsFirstFreeIpAfterRollover()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var assignments = await stateStore.For<IpAssignment>().ListAsync();
            await stateStore.For<IpAssignment>().DeleteRangeAsync(assignments.GetRange(1, 2));

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var assignment = result.Should().BeRight().Subject;
            assignment.IpAddress.Should().Be("10.0.0.101");

            var ipPool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            ipPool!.Counter.Should().Be(2);
        });
    }

    // This test also ensures that the logic terminates
    [Fact]
    public async Task AcquireIp_AllIpAddressesInUse_ReturnsError()
    {
        await WithScope(async (poolManager, stateStore) =>
        {
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();
            (await poolManager.AcquireIp(SubnetId, IpPoolName)).Should().BeRight();

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (poolManager, stateStore) =>
        {
            var result = await poolManager.AcquireIp(SubnetId, IpPoolName);

            var error = result.Should().BeLeft().Subject;
            error.Message.Should().Be($"IP pool {IpPoolName}({IpPoolId}) has no more free IP addresses.");
        });
    }

    private async Task WithScope(Func<IIpPoolManager, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var poolManager = scope.GetInstance<IIpPoolManager>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(poolManager, stateStore);
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
                                LastIp = "10.0.0.105",
                            }
                        ]
                    },
                ]
            });
    }
}