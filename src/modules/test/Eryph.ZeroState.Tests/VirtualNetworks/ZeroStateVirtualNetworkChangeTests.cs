using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.ZeroState.Tests.VirtualNetworks;

public class ZeroStateVirtualNetworkChangeTests : ZeroStateTestBase
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid NetworkId = Guid.NewGuid();
    private static readonly Guid SubnetId = Guid.NewGuid();
    private static readonly Guid IpPoolId = Guid.NewGuid();

    private readonly ProjectNetworksConfig _expectedConfig = new()
    {
        Version = "1.0",
        Project = "Test Project",
        Networks =
        [
            new NetworkConfig()
            {
                Name = "Test Network",
                Subnets =
                [
                    new NetworkSubnetConfig()
                    {
                        Name = "Test Subnet",
                        Address = "10.0.0.0/20",
                        Mtu = 1400,
                        IpPools =
                        [
                            new IpPoolConfig()
                            {
                                Name = "Test Pool",
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

    [Fact]
    public async Task Network_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = new VirtualNetwork()
            {
                Name = "New Network",
                ProjectId = ProjectId,
                IpNetwork = "10.1.0.0/20",
            };
            await stateStore.For<VirtualNetwork>().AddAsync(network);
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks =
        [
            .._expectedConfig.Networks!,
            new NetworkConfig()
            {
                Name = "New Network",
                Address = "10.1.0.0/20",
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Network_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(NetworkId);
            network!.IpNetwork = "10.1.0.0/20";
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Address = "10.1.0.0/20";
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Network_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(NetworkId);
            await stateStore.For<VirtualNetwork>().DeleteAsync(network!);
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks = [];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpPool_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(SubnetId);
            await stateStore.LoadCollectionAsync(subnet!, s => s.IpPools);
            subnet!.IpPools!.Add(new IpPool()
            {
                Name = "New Pool",
                FirstIp = "10.0.1.100",
                NextIp = "10.0.1.110",
                LastIp = "10.0.1.200",
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets![0].IpPools =
        [
            .._expectedConfig.Networks![0].Subnets![0].IpPools,
            new IpPoolConfig()
            {
                Name = "New Pool",
                FirstIp = "10.0.1.100",
                NextIp = "10.0.1.110",
                LastIp = "10.0.1.200",
            }
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpPool_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            pool!.NextIp = "10.0.0.111";
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets![0].IpPools![0].NextIp = "10.0.0.111";
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task IpPool_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var pool = await stateStore.For<IpPool>().GetByIdAsync(IpPoolId);
            await stateStore.For<IpPool>().DeleteAsync(pool!);
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets![0].IpPools = null;
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Subnet_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var network = await stateStore.For<VirtualNetwork>().GetByIdAsync(NetworkId);
            await stateStore.LoadCollectionAsync(network!, s => s.Subnets);
            network!.Subnets!.Add(new VirtualNetworkSubnet()
            {
                Name = "New Subnet",
                IpNetwork = "10.0.100.0/20",
                MTU = 1200,
            });

            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets =
        [
            .._expectedConfig.Networks![0].Subnets!,
            new NetworkSubnetConfig()
            {
                Name = "New Subnet",
                Address = "10.0.100.0/20",
                Mtu = 1200,
            },
        ];
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Subnet_delete_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(SubnetId);
            await stateStore.For<VirtualNetworkSubnet>().DeleteAsync(subnet!);
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets = null;
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    [Fact]
    public async Task Subnet_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnet = await stateStore.For<VirtualNetworkSubnet>().GetByIdAsync(SubnetId);
            subnet!.MTU = 1300;
            await stateStore.SaveChangesAsync();
        });

        var config = await ReadConfig();
        _expectedConfig.Networks![0].Subnets![0].Mtu = 1300;
        config.Should().BeEquivalentTo(_expectedConfig);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<Project>().AddAsync(new Project()
        {
            Id = ProjectId,
            Name = "Test Project",
            TenantId = EryphConstants.DefaultTenantId,
        });

        await stateStore.For<VirtualNetwork>().AddAsync(new VirtualNetwork()
        {
            Id = NetworkId,
            Name = "Test Network",
            ProjectId = ProjectId,
            Subnets =
            [
                new VirtualNetworkSubnet()
                {
                    Id = SubnetId,
                    Name = "Test Subnet",
                    IpNetwork = "10.0.0.0/20",
                    MTU = 1400,
                    IpPools =
                    [
                        new IpPool()
                        {
                            Id = IpPoolId,
                            Name = "Test Pool",
                            FirstIp = "10.0.0.100",
                            NextIp = "10.0.0.110",
                            LastIp = "10.0.0.200",
                        },
                    ],
                },
            ],
        });

        await stateStore.For<CatletNetworkPort>().AddAsync(new CatletNetworkPort()
        {

        });
    }

    private async Task<CatletNetworkPortsConfigModel> ReadConfig()
    {
        var path = Path.Combine(
            ZeroStateConfig.ProjectNetworksConfigPath,
            $"{ProjectId}.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<CatletNetworkPortsConfigModel>(json)!;
    }
}
