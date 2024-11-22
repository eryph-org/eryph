using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

[Trait("Category", "Docker")]
[Collection(nameof(MySqlDatabaseCollection))]
public class MySqlVirtualNetworkChangeTrackingTests(
    MySqlFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : VirtualNetworkChangeTrackingTests(databaseFixture, outputHelper);

[Collection(nameof(SqliteDatabaseCollection))]
public class SqliteVirtualNetworkChangeTrackingTests(
    SqliteFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : VirtualNetworkChangeTrackingTests(databaseFixture, outputHelper);

public abstract class VirtualNetworkChangeTrackingTests(
    IDatabaseFixture databaseFixture,
    ITestOutputHelper outputHelper)
    : ChangeTrackingTestBase(databaseFixture, outputHelper)
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid VirtualNetworkId = Guid.NewGuid();
    private static readonly Guid VirtualSubnetId = Guid.NewGuid();
    private static readonly Guid VirtualIpPoolId = Guid.NewGuid();
    private static readonly Guid FloatingPortId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();
    private static readonly Guid CatletPortId = Guid.NewGuid();
    private static readonly Guid IpAssignmentId = Guid.NewGuid();

    private readonly ProjectNetworksConfig _expectedNetworksConfig = new()
    {
        Version = "1.0",
        Project = "test-project",
        Networks =
        [
            new NetworkConfig()
            {
                Name = "virtual-test-network",
                Environment = "test-environment",
                Subnets =
                [
                    new NetworkSubnetConfig()
                    {
                        Name = "virtual-test-subnet",
                        Address = "10.0.0.0/20",
                        Mtu = 1400,
                        IpPools =
                        [
                            new IpPoolConfig()
                            {
                                Name = "virtual-test-pool",
                                FirstIp = "10.0.0.100",
                                NextIp = "10.0.0.110",
                                LastIp = "10.0.0.200",
                            },
                        ],
                    },
                ],
            },
        ],
    };

    private readonly CatletNetworkPortsConfigModel _expectedPortsConfig = new()
    {
        CatletNetworkPorts =
        [
            new CatletNetworkPortConfigModel()
            {
                CatletMetadataId = CatletMetadataId,
                Name = "test-catlet-port",
                VirtualNetworkName = "virtual-test-network",
                EnvironmentName = "test-environment",
                MacAddress = "42:00:42:00:00:01",
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
                MacAddress = "42:00:42:00:00:02",
                CatletMetadataId = CatletMetadataId,
                NetworkId = VirtualNetworkId,
            });

            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts =
        [
            .._expectedPortsConfig.CatletNetworkPorts,
            new CatletNetworkPortConfigModel()
            {
                CatletMetadataId = CatletMetadataId,
                Name = "new-catlet-port",
                MacAddress = "42:00:42:00:00:02",
                VirtualNetworkName = "virtual-test-network",
                EnvironmentName = "test-environment",
                FloatingNetworkPort = null,
                IpAssignments = [],
            },
        ];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task CatletPort_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var catletPort = await stateStore.For<CatletNetworkPort>().GetByIdAsync(CatletPortId);
            catletPort!.AddressName = "test";
            catletPort!.MacAddress = "42:00:42:00:00:02";

            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].AddressName = "test";
        _expectedPortsConfig.CatletNetworkPorts[0].MacAddress = "42:00:42:00:00:02";
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].IpAssignments =
        [
            .._expectedPortsConfig.CatletNetworkPorts[0].IpAssignments,
            new IpAssignmentConfigModel()
            {
                IpAddress = "10.0.0.150",
                SubnetName = "virtual-test-subnet",
                PoolName = "virtual-test-pool",
            },
        ];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].IpAssignments[0].IpAddress = "10.0.0.110";
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        var networksConfig = await ReadNetworksConfig();
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].IpAssignments = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Network_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = new VirtualNetwork()
            {
                Name = "new-network",
                ProjectId = ProjectId,
                Environment = EryphConstants.DefaultEnvironmentName,
                IpNetwork = "10.1.0.0/20",
            };
            await stateStore.For<VirtualNetwork>().AddAsync(network);
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks =
        [
            .._expectedNetworksConfig.Networks!,
            new NetworkConfig()
            {
                Name = "new-network",
                Address = "10.1.0.0/20",
            },
        ];
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Network_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(VirtualNetworkId);
            network!.IpNetwork = "10.1.0.0/20";
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Address = "10.1.0.0/20";
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Network_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(VirtualNetworkId);
            await stateStore.For<VirtualNetwork>().DeleteAsync(network!);
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks = [];
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task IpPool_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(VirtualSubnetId);
            await stateStore.LoadCollectionAsync(subnet!, s => s.IpPools);
            subnet!.IpPools!.Add(new IpPool()
            {
                Name = "new-pool",
                FirstIp = "10.0.1.100",
                NextIp = "10.0.1.110",
                LastIp = "10.0.1.200",
            });

            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets![0].IpPools =
        [
            .._expectedNetworksConfig.Networks![0].Subnets![0].IpPools,
            new IpPoolConfig()
            {
                Name = "new-pool",
                FirstIp = "10.0.1.100",
                NextIp = "10.0.1.110",
                LastIp = "10.0.1.200",
            }
        ];
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task IpPool_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(VirtualIpPoolId);
            pool!.NextIp = "10.0.0.111";
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets![0].IpPools![0].NextIp = "10.0.0.111";
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task IpPool_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(VirtualIpPoolId);
            await stateStore.For<IpPool>().DeleteAsync(pool!);
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets![0].IpPools = null;
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].IpAssignments = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Subnet_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(VirtualNetworkId);
            await stateStore.LoadCollectionAsync(network!, s => s.Subnets);
            network!.Subnets!.Add(new VirtualNetworkSubnet()
            {
                Name = "new-subnet",
                IpNetwork = "10.0.100.0/20",
                MTU = 1200,
            });

            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets =
        [
            .._expectedNetworksConfig.Networks![0].Subnets!,
            new NetworkSubnetConfig()
            {
                Name = "new-subnet",
                Address = "10.0.100.0/20",
                Mtu = 1200,
            },
        ];
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Subnet_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(VirtualSubnetId);
            await stateStore.For<VirtualNetworkSubnet>().DeleteAsync(subnet!);
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets = null;
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.CatletNetworkPorts[0].IpAssignments = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task Subnet_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(VirtualSubnetId);
            subnet!.DnsDomain = "eryph.invalid";
            subnet!.MTU = 1300;
            await stateStore.SaveChangesAsync();
        });

        var networksConfig = await ReadNetworksConfig();
        _expectedNetworksConfig.Networks![0].Subnets![0].DnsDomain = "eryph.invalid";
        _expectedNetworksConfig.Networks![0].Subnets![0].Mtu = 1300;
        networksConfig.Should().BeEquivalentTo(_expectedNetworksConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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
    }

    private async Task<ProjectNetworksConfig> ReadNetworksConfig()
    {
        var path = Path.Combine(
            ChangeTrackingConfig.ProjectNetworksConfigPath,
            $"{ProjectId}.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return ProjectNetworksConfigJsonSerializer.Deserialize(json);
    }

    private async Task<CatletNetworkPortsConfigModel> ReadPortsConfig()
    {
        var path = Path.Combine(
            ChangeTrackingConfig.ProjectNetworkPortsConfigPath,
            $"{ProjectId}.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<CatletNetworkPortsConfigModel>(json)!;
    }
}
