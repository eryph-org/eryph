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
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector;

namespace Eryph.Modules.Controller.Tests.Networks;

public class ProviderIpManagerTests : InMemoryStateDbTestBase
{
    private const string DefaultSubnetId = "00bbb738-9b76-4b52-8c9a-89fcb2516f66";
    private const string SecondSubnetId = "edd1c7e0-c8e5-4679-b98d-f7672914d5f7";
    private const string SecondProviderSubnetId = "f712ba4e-ace9-4830-a346-59d17c11b764";

    private static readonly Guid FloatingPortId = Guid.NewGuid();

    [Theory]
    [InlineData("default", "default", "default", "10.0.0.12")]
    [InlineData("default", "default", "second-pool", "10.0.1.12")]
    [InlineData("default", "second-subnet", "default", "10.1.0.12")]
    [InlineData("second-provider", "default", "default", "10.10.0.12")]
    public async Task ConfigureFloatingPortIps_NewPortIsAdded_AssignmentIsCreated(
        string providerName,
        string subnetName,
        string poolName,
        string expectedIpAddress)
    {
        await WithScope(async (providerIpManager, _, stateStore) =>
        {
            var floatingPort = new FloatingNetworkPort
            {
                Id = FloatingPortId,
                Name = "test-floating-port",
                MacAddress = "42:00:42:00:00:01",
                ProviderName = providerName,
                SubnetName = subnetName,
                PoolName = poolName,
            };
            await stateStore.For<FloatingNetworkPort>().AddAsync(floatingPort);

            var result = await providerIpManager.ConfigureFloatingPortIps(
                providerName, floatingPort);

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
                    ipAssignment.NetworkPortId.Should().Be(FloatingPortId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);
                });
        });
    }

    [Theory]
    [InlineData("default", DefaultSubnetId, "default", "default", "10.0.0.12")]
    [InlineData("default", DefaultSubnetId, "default", "second-pool", "10.0.1.12")]
    [InlineData("default", SecondSubnetId, "second-subnet", "default", "10.1.0.12")]
    [InlineData("second-provider", SecondProviderSubnetId, "default", "default", "10.10.0.12")]
    public async Task ConfigureFloatingPortIps_AssignmentIsValid_AssignmentIsNotChanged(
        string providerName,
        string subnetId,
        string subnetName,
        string poolName,
        string expectedIpAddress)
    {
        var ipAssignmentId = Guid.Empty; 
        await WithScope(async (_, ipPoolManager, stateStore) =>
        {
            var ipAssignmentResult = ipPoolManager.AcquireIp(
                Guid.Parse(subnetId),
                poolName);
            var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
            ipAssignment.IpAddress.Should().Be(expectedIpAddress);
            ipAssignmentId = ipAssignment.Id;

            var floatingPort = new FloatingNetworkPort
            {
                Id = FloatingPortId,
                Name = "test-floating-port",
                MacAddress = "42:00:42:00:00:01",
                ProviderName = providerName,
                SubnetName = subnetName,
                PoolName = poolName,
                IpAssignments = [ipAssignment]
            };
            await stateStore.For<FloatingNetworkPort>().AddAsync(floatingPort);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (providerIpManager, _, stateStore) =>
        {
            var floatingPort = await stateStore.Read<FloatingNetworkPort>()
                .GetByIdAsync(FloatingPortId);
            floatingPort.Should().NotBeNull();

            var result = await providerIpManager.ConfigureFloatingPortIps(
                providerName, floatingPort!);

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
                    ipAssignment.NetworkPortId.Should().Be(FloatingPortId);
                    ipAssignment.Id.Should().Be(ipAssignmentId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);
                });
        });
    }

    [Theory]
    [InlineData("default", "default", "second-pool", "10.0.1.12")]
    [InlineData("default", "second-subnet", "default", "10.1.0.12")]
    [InlineData("second-provider", "default", "default", "10.10.0.12")]
    public async Task ConfigureFloatingPortIps_AssignmentIsInvalid_AssignmentIsChanged(
        string providerName,
        string subnetName,
        string poolName,
        string expectedIpAddress)
    {
        var ipAssignmentId = Guid.Empty;
        await WithScope(async (_, ipPoolManager, stateStore) =>
        {
            var ipAssignmentResult = ipPoolManager.AcquireIp(
                Guid.Parse(DefaultSubnetId),
                "default");
            var ipAssignment = ipAssignmentResult.Should().BeRight().Subject;
            ipAssignment.IpAddress.Should().Be("10.0.0.12");
            ipAssignmentId = ipAssignment.Id;

            var floatingPort = new FloatingNetworkPort
            {
                Id = FloatingPortId,
                Name = "test-floating-port",
                MacAddress = "42:00:42:00:00:01",
                ProviderName = "default",
                SubnetName = "default",
                PoolName = "default",
                IpAssignments = [ipAssignment]
            };
            await stateStore.For<FloatingNetworkPort>().AddAsync(floatingPort);
            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (providerIpManager, _, stateStore) =>
        {
            var floatingPort = await stateStore.For<FloatingNetworkPort>()
                .GetByIdAsync(FloatingPortId);
            floatingPort.Should().NotBeNull();

            floatingPort!.ProviderName = providerName;
            floatingPort.SubnetName = subnetName;
            floatingPort.PoolName = poolName;

            var result = await providerIpManager.ConfigureFloatingPortIps(
                providerName, floatingPort);

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
                    ipAssignment.NetworkPortId.Should().Be(FloatingPortId);
                    ipAssignment.Id.Should().NotBe(ipAssignmentId);
                    ipAssignment.IpAddress.Should().Be(expectedIpAddress);

                });
        });
    }

    private async Task WithScope(Func<IProviderIpManager, IIpPoolManager, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var catletIpManager = scope.GetInstance<IProviderIpManager>();
        var ipPoolManager = scope.GetInstance<IIpPoolManager>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(catletIpManager, ipPoolManager, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        // Use the proper IpPoolManager instead of a mock. The code is
        // quite interdependent as it modifies the same EF Core entities.
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        options.Container.Register<IProviderIpManager, ProviderIpManager>(Lifestyle.Scoped);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<ProviderSubnet>().AddAsync(
            new ProviderSubnet
            {
                Id = Guid.Parse(DefaultSubnetId),
                ProviderName = EryphConstants.DefaultProviderName,
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
                ]
            });

        await stateStore.For<ProviderSubnet>().AddAsync(
            new ProviderSubnet
            {
                Id = Guid.Parse(SecondSubnetId),
                ProviderName = EryphConstants.DefaultProviderName,
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
                    },
                ],
            });

        await stateStore.For<ProviderSubnet>().AddAsync(
            new ProviderSubnet
            {
                Id = Guid.Parse(SecondProviderSubnetId),
                ProviderName = "second-provider",
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
                    },
                ],
            });
    }
}
