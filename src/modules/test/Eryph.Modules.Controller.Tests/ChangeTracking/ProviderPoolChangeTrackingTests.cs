using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using SimpleInjector.Integration.ServiceCollection;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

public class ProviderPoolChangeTrackingTests : ChangeTrackingTestBase
{
    [Fact]
    public async Task IpPool_update_is_detected()
    {
        MockNetworkProviderManager.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, NetworkProvidersConfiguration>(GetConfig()));

        NetworkProvidersConfiguration? savedConfig = null;
        MockNetworkProviderManager.Setup(
            m => m.SaveConfiguration(It.IsAny<NetworkProvidersConfiguration>()))
            .Returns(RightAsync<Error, Unit>(unit))
            .Callback((NetworkProvidersConfiguration c) => savedConfig = c);

        await WithHostScope(async stateStore =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync();
            await stateStore.LoadCollectionAsync(subnets[0], s => s.IpPools);
            subnets[0].IpPools[0].NextIp = "10.0.0.150";

            await stateStore.SaveChangesAsync();
        });

        var expectedConfig = GetConfig();
        expectedConfig.NetworkProviders[0].Subnets[0].IpPools[0].NextIp = "10.0.0.150";
        savedConfig.Should().BeEquivalentTo(expectedConfig);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        await using var scope = CreateScope();
        await scope.GetInstance<INetworkProvidersConfigRealizer>()
            .RealizeConfigAsync(GetConfig(), default);
        await scope.GetInstance<IStateStore>().SaveChangesAsync();
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>();
    }

    private NetworkProvidersConfiguration GetConfig() => new()
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
