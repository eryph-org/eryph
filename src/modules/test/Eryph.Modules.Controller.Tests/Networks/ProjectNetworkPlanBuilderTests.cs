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
    private const string DefaultProjectId = "4b4a3fcf-b5ed-4a9a-ab6e-03852752095e";
    private const string SecondProjectId = "75c27daf-77c8-4b98-a072-a4706dceb422";

    private const string DefaultNetworkId = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
    private const string DefaultSubnetId = "ed6697cd-836f-4da7-914b-b09ed1567934";
    private const string SecondSubnetId = "4f976208-613a-40d4-a284-d32cbd4a1b8e";

    private const string SecondNetworkId = "e480a020-57d0-4443-a973-57aa0c95872e";
    private const string SecondNetworkSubnetId = "27ec11a4-5d6a-47da-9f9f-eb7486db38ea";

    private const string ThirdNetworkId = "9016fa5b-e0c7-4626-b1ba-6dc21902d04f";
    private const string ThirdNetworkSubnetId = "106fa5c1-8cf1-4ccd-915a-f9dc230cc299";

    private const string SecondEnvironmentNetworkId = "81a139e5-ab61-4fe3-b81f-59c11a665d22";
    private const string SecondEnvironmentSubnetId = "dc807357-50e7-4263-8298-0c97ff69f4cf";

    private const string SecondProjectNetworkId = "c0043e88-8268-4ac0-b027-2fa37ad3168f";
    private const string SecondProjectSubnetId = "0c721846-5e2e-40a9-83d2-f1b75206ef84";

    private const string FlatNetworkId = "98ff838a-a2c3-464d-8884-f348888ed804";

    private const string CatletMetadataId = "15e2b061-c625-4469-9fe7-7c455058fcc0";

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
                Guid.Parse(DefaultProjectId),
                _networkProvidersConfig);

            var networkPlan = result.Should().BeRight().Subject;

            networkPlan.Id.Should().Be(DefaultProjectId);
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