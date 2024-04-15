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

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

public class CatletNetworkPortChangeTrackingTests : ChangeTrackingTestBase
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid VirtualNetworkId = Guid.NewGuid();
    private static readonly Guid VirtualSubnetId = Guid.NewGuid();
    private static readonly Guid VirtualIpPoolId = Guid.NewGuid();
    private static readonly Guid FloatingPortId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
    private static readonly Guid CatletPortId = Guid.NewGuid();
    private static readonly Guid IpAssignmentId = Guid.NewGuid();

    private readonly CatletNetworkPortsConfigModel _expectedConfig = new()
    {
        CatletNetworkPorts =
        [
            new CatletNetworkPortConfigModel()
            {
                CatletMetadataId = CatletMetadataId,
                Name = "test-catlet-port",
                VirtualNetworkName = "virtual-test-network",
                EnvironmentName = "test-environment",
                MacAddress = "00:00:00:00:00:01",
                FloatingNetworkPort = new()
                {
                    Name = "test-floating-port",
                    ProviderName = "test-provider",
                    SubnetName = "provider-test-subnet",
                },
                IpAssignments =
                [
                    new IpAssignmentConfigModel()
                    {
                        IpAddress = "10.0.0.104",
                        SubnetName = "virtual-test-subnet",
                        PoolName = "virtual-test-pool",
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task CatletPort_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            await stateStore.For<CatletNetworkPort>().AddAsync(new CatletNetworkPort()
            {
                Name = "new-catlet-port",
                CatletMetadataId = CatletMetadataId,
                NetworkId = VirtualNetworkId,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.CatletNetworkPorts =
        [
            .._expectedConfig.CatletNetworkPorts,
            new CatletNetworkPortConfigModel()
            {
                CatletMetadataId = CatletMetadataId,
                Name = "new-catlet-port",
                VirtualNetworkName = "virtual-test-network",
                EnvironmentName = "test-environment",
                FloatingNetworkPort = null,
                IpAssignments = [],
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task CatletPort_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var catletPort = await stateStore.For<CatletNetworkPort>().GetByIdAsync(CatletPortId);
            catletPort!.MacAddress = "00:00:00:00:00:02";

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.CatletNetworkPorts[0].MacAddress = "00:00:00:00:00:02";
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task CatletPort_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var catletPort = await stateStore.For<CatletNetworkPort>().GetByIdAsync(CatletPortId);
            await stateStore.For<CatletNetworkPort>().DeleteAsync(catletPort!);

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.CatletNetworkPorts = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpAssignment_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var catletPort = await stateStore.For<CatletNetworkPort>().GetByIdAsync(CatletPortId);
            await stateStore.LoadCollectionAsync(catletPort!, p => p.IpAssignments);
            catletPort!.IpAssignments.Add(new IpPoolAssignment()
            {
                IpAddress = "10.0.0.150",
                SubnetId = VirtualSubnetId,
                PoolId = VirtualIpPoolId,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.CatletNetworkPorts[0].IpAssignments =
        [
            .._expectedConfig.CatletNetworkPorts[0].IpAssignments,
            new IpAssignmentConfigModel()
            {
                IpAddress = "10.0.0.150",
                SubnetName = "virtual-test-subnet",
                PoolName = "virtual-test-pool",
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
        _expectedConfig.CatletNetworkPorts[0].IpAssignments[0].IpAddress = "10.0.0.110";
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
        _expectedConfig.CatletNetworkPorts[0].IpAssignments = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

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
    }

    private async Task<CatletNetworkPortsConfigModel> ReadConfig()
    {
        var path = Path.Combine(
            ChangeTrackingConfig.ProjectNetworkPortsConfigPath,
            $"{ProjectId}.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<CatletNetworkPortsConfigModel>(json)!;
    }
}
