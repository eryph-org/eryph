using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb.Model;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector;
using Xunit.Abstractions;
using System.Linq.Expressions;


namespace Eryph.Modules.Controller.Tests.Networks;

public class ProjectNetworkPlanBuilderTests(
    ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private readonly NetworkProvidersConfiguration _networkProvidersConfig = new()
    {
        NetworkProviders =
        [
            new NetworkProvider
            {
                Name = "default",
                TypeString = "nat_overlay",
                BridgeName = "br-nat",
                Subnets =
                [
                    new NetworkProviderSubnet
                    {
                        Name = "default",
                        Network = "10.249.248.0/22",
                        Gateway = "10.249.248.1",
                        IpPools =
                        [
                            new NetworkProviderIpPool
                            {
                                Name = "default",
                                FirstIp = "10.249.248.10",
                                NextIp = "10.249.248.10",
                                LastIp = "10.249.251.241",
                            },
                        ],
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task GenerateNetworkPlan_DefaultConfig_GeneratesValidNetworkPlan()
    {
        await using(var scope = CreateScope())
        {
            var providerConfigRealizer = scope.GetInstance<INetworkProvidersConfigRealizer>();
            await providerConfigRealizer.RealizeConfigAsync(_networkProvidersConfig, default);

            var networkConfig = new ProjectNetworksConfig()
            {
                Networks =
                [
                    new NetworkConfig()
                    {
                        Name = EryphConstants.DefaultNetworkName,
                        Address = "10.0.100.0/24",
                    },
                ],
            };

            var configRealizer = scope.GetInstance<INetworkConfigRealizer>();
            await configRealizer.UpdateNetwork(EryphConstants.DefaultProjectId, networkConfig, _networkProvidersConfig);

            var stateStore = scope.GetInstance<IStateStore>();
            await stateStore.SaveChangesAsync();
        }

        await WithScope(async (builder, _) =>
        {
            var result = await builder.GenerateNetworkPlan(
                EryphConstants.DefaultProjectId,
                _networkProvidersConfig);

            var networkPlan = result.Should().BeRight().Subject;

            networkPlan.Id.Should().Be(EryphConstants.DefaultProjectId.ToString());
            networkPlan.PlannedSwitchPorts.ToDictionary().Should().ContainKey(
                $"SN-externalNet-{networkPlan.Id}-default-default-br-nat");
        });
    }

    private async Task WithScope(Func<ProjectNetworkPlanBuilder, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var builder = scope.GetInstance<ProjectNetworkPlanBuilder>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(builder, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        options.Container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
        options.Container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>(Lifestyle.Scoped);

        options.Container.Register<ProjectNetworkPlanBuilder>(Lifestyle.Scoped);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();
    }
}
