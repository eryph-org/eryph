using System.Text;
using System.Text.Json;
using Eryph.Configuration.Model;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using SimpleInjector.Integration.ServiceCollection;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

public class NetworkProvidersChangeTrackingTestsWithTransaction()
    : NetworkProvidersChangeTrackingTests(AutoTransactionBehavior.Always);

public class NetworkProvidersChangeTrackingTestsWithoutTransaction()
    : NetworkProvidersChangeTrackingTests(AutoTransactionBehavior.WhenNeeded);

public abstract class NetworkProvidersChangeTrackingTests : ChangeTrackingTestBase
{
    private static readonly Guid FloatingPortId = Guid.NewGuid();
    private static readonly Guid IpAssignmentId = Guid.NewGuid();

    private readonly FloatingNetworkPortsConfigModel _expectedPortsConfig = new()
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

    private NetworkProvidersConfiguration? _savedProvidersConfig;

    protected NetworkProvidersChangeTrackingTests(
        AutoTransactionBehavior autoTransactionBehavior)
        : base(autoTransactionBehavior)
    {
        MockNetworkProviderManager.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, NetworkProvidersConfiguration>(GetProvidersConfig()));

        MockNetworkProviderManager.Setup(
                m => m.SaveConfiguration(It.IsAny<NetworkProvidersConfiguration>()))
            .Returns(RightAsync<Error, Unit>(unit))
            .Callback((NetworkProvidersConfiguration c) => _savedProvidersConfig = c);
    }

    [Fact]
    public async Task IpPool_update_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync();
            await stateStore.LoadCollectionAsync(subnets[0], s => s.IpPools);
            subnets[0].IpPools[0].NextIp = "10.0.0.150";

            await stateStore.SaveChangesAsync();
        });

        var expectedConfig = GetProvidersConfig();
        expectedConfig.NetworkProviders[0].Subnets[0].IpPools[0].NextIp = "10.0.0.150";
        _savedProvidersConfig.Should().BeEquivalentTo(expectedConfig);
        var portsConfig = await ReadPortsConfig();
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

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

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts =
        [
            .._expectedPortsConfig.FloatingPorts,
            new FloatingNetworkPortConfigModel()
            {
                Name = "new-floating-port",
                ProviderName = "test-provider",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
                IpAssignments = [],
            },
        ];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts[0].MacAddress = "00:00:00:00:00:02";
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
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

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    [Fact]
    public async Task IpAssignment_new_is_detected()
    {
        await WithHostScope(async stateStore =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(
                new ProviderSubnetSpecs.GetForChangeTracking());
            var floatingPort = await stateStore.For<FloatingNetworkPort>().GetByIdAsync(FloatingPortId);
            await stateStore.LoadCollectionAsync(floatingPort!, p => p.IpAssignments);
            floatingPort!.IpAssignments.Add(new IpPoolAssignment()
            {
                IpAddress = "10.0.0.150",
                SubnetId = subnets[0].Id,
                PoolId = subnets[0].IpPools[0].Id,
            });

            await stateStore.SaveChangesAsync();
        });

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts[0].IpAssignments =
        [
            .._expectedPortsConfig.FloatingPorts[0].IpAssignments,
            new IpAssignmentConfigModel()
            {
                IpAddress = "10.0.0.150",
                SubnetName = "provider-test-subnet",
                PoolName = "provider-test-pool",
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

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts[0].IpAssignments[0].IpAddress = "10.0.0.110";
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

        _savedProvidersConfig.Should().BeEquivalentTo(GetProvidersConfig());
        var portsConfig = await ReadPortsConfig();
        _expectedPortsConfig.FloatingPorts[0].IpAssignments = [];
        portsConfig.Should().BeEquivalentTo(_expectedPortsConfig);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        await using (var scope = CreateScope())
        {
            await scope.GetInstance<INetworkProvidersConfigRealizer>()
                .RealizeConfigAsync(GetProvidersConfig(), default);
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
                MacAddress = "00:00:00:00:00:01",
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
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>();
    }

    private async Task<FloatingNetworkPortsConfigModel> ReadPortsConfig()
    {
        var path = Path.Combine(ChangeTrackingConfig.NetworksConfigPath, "ports.json");
        var json = await MockFileSystem.File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<FloatingNetworkPortsConfigModel>(json)!;
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
