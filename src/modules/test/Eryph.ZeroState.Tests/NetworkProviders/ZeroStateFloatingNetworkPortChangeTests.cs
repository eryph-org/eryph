using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.ZeroState.Tests.NetworkProviders;

public class ZeroStateFloatingNetworkPortChangeTests : ZeroStateTestBase
{
    private static readonly Guid ProviderSubnetId = Guid.NewGuid();
    private static readonly Guid ProviderIpPoolId = Guid.NewGuid();
    private static readonly Guid FloatingPortId = Guid.NewGuid();
    private static readonly Guid IpAssignmentId = Guid.NewGuid();

    private readonly FloatingNetworkPortsConfigModel _expectedConfig = new()
    {
        FloatingPorts = 
        [
            new FloatingNetworkPortConfigModel()
            {
                Name = "test-floating-port",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
                MacAddress = "00:00:00:00:00:01",
                IpAssignments =
                [
                    new IpAssignmentConfigModel()
                    {
                        IpAddress = "10.0.0.104",
                        SubnetName = "provider-test-subnet",
                        PoolName = "provider-test-pool",
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task FloatingPort_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            await stateStore.For<FloatingNetworkPort>().AddAsync(new FloatingNetworkPort()
            {
                Name = "new-floating-port",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts =
        [
            .._expectedConfig.FloatingPorts,
            new FloatingNetworkPortConfigModel()
            {
                Name = "new-floating-port",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
                IpAssignments = [],
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task FloatingPort_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var floatingPort = await stateStore.For<FloatingNetworkPort>().GetByIdAsync(FloatingPortId);
            floatingPort!.MacAddress = "00:00:00:00:00:02";

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts[0].MacAddress = "00:00:00:00:00:02";
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task FloatingPort_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var floatingPort = await stateStore.For<FloatingNetworkPort>().GetByIdAsync(FloatingPortId);
            await stateStore.For<FloatingNetworkPort>().DeleteAsync(floatingPort!);

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpAssignment_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var floatingPort = await stateStore.For<FloatingNetworkPort>().GetByIdAsync(FloatingPortId);
            await stateStore.LoadCollectionAsync(floatingPort!, p => p.IpAssignments);
            floatingPort!.IpAssignments.Add(new IpPoolAssignment()
            {
                IpAddress = "10.0.0.150",
                SubnetId = ProviderSubnetId,
                PoolId = ProviderIpPoolId,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts[0].IpAssignments =
        [
            .._expectedConfig.FloatingPorts[0].IpAssignments,
            new IpAssignmentConfigModel()
            {
                IpAddress = "10.0.0.150",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpAssignment_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var assignment = await stateStore.For<IpPoolAssignment>().GetByIdAsync(IpAssignmentId);
            assignment!.IpAddress = "10.0.0.110";

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts[0].IpAssignments[0].IpAddress = "10.0.0.110";
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpAssignment_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var assignment = await stateStore.For<IpPoolAssignment>().GetByIdAsync(IpAssignmentId);
            await stateStore.For<IpPoolAssignment>().DeleteAsync(assignment!);

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.FloatingPorts[0].IpAssignments = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
        
        await stateStore.For<ProviderSubnet>().AddAsync(new ProviderSubnet()
        {
            Id = ProviderSubnetId,
            Name = "provider-test-subnet",
            ProviderName = "test-provider",
            IpPools =
            [
                new IpPool()
                {
                    Id = ProviderIpPoolId,
                    Name = "provider-test-pool",
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
            MacAddress = "00:00:00:00:00:01",
            IpAssignments =
            [
                new IpPoolAssignment()
                {
                    Id = IpAssignmentId,
                    SubnetId = ProviderSubnetId,
                    PoolId = ProviderIpPoolId,
                    IpAddress = "10.0.0.104",
                    Number = 5,
                },
            ],
        });
    }

    private async Task<FloatingNetworkPortsConfigModel> ReadConfig()
    {
        var path = Path.Combine(ZeroStateConfig.NetworksConfigPath, "ports.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<FloatingNetworkPortsConfigModel>(json)!;
    }
}