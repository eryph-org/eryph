﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
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

    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();

    [Fact]
    public async Task BuildPLan()
    {
        await WithScope(async (builder, _) =>
        {
            var result = await builder.GenerateNetworkPlan(Guid.Parse(DefaultProjectId), default);

            var networkPlan = result.Should().BeRight().Subject;

            networkPlan.Id.Should().Be($"project-{DefaultProjectId}");
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
        options.Container.RegisterInstance(_networkProviderManagerMock.Object);

        // Use the proper manager instead of a mock. The code is quite
        // interdependent as it modifies the same EF Core entities.
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);

        options.Container.Register<ProjectNetworkPlanBuilder>(Lifestyle.Scoped);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var networkProvidersConfig = new NetworkProvidersConfiguration
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
                            Network = "10.249.248.0/24",
                            Gateway = "10.249.248.1",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = "default",
                                    FirstIp = "10.249.248.10",
                                    NextIp = "10.249.248.12",
                                    LastIp = "10.249.248.19"
                                },
                                new NetworkProviderIpPool
                                {
                                    Name = "second-provider-pool",
                                    FirstIp = "10.249.248.20",
                                    NextIp = "10.249.248.22",
                                    LastIp = "10.249.248.29"
                                },
                            ],
                        },
                        new NetworkProviderSubnet
                        {
                            Name = "second-provider-subnet",
                            Network = "10.249.249.0/24",
                            Gateway = "10.249.249.1",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = "default",
                                    FirstIp = "10.249.249.10",
                                    NextIp = "10.249.249.12",
                                    LastIp = "10.249.249.19"
                                },
                            ],
                        },
                    ],
                },
                new NetworkProvider
                {
                    Name = "second-overlay-provider",
                    TypeString = "overlay",
                    BridgeName = "br-second-nat",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Network = "10.249.248.0/24",
                            Gateway = "10.249.248.1",
                            IpPools =
                            [
                                new NetworkProviderIpPool
                                {
                                    Name = "default",
                                    FirstIp = "10.249.248.10",
                                    NextIp = "10.249.248.12",
                                    LastIp = "10.249.248.19"
                                },
                                new NetworkProviderIpPool
                                {
                                    Name = "second-provider-pool",
                                    FirstIp = "10.249.248.20",
                                    NextIp = "10.249.248.22",
                                    LastIp = "10.249.248.29"
                                },
                            ],
                        },
                    ],
                },
                new NetworkProvider
                {
                    Name = "flat-provider",
                    TypeString = "flat",
                },
            ]
        };

        _networkProviderManagerMock
            .Setup(m => m.GetCurrentConfiguration())
            .Returns(Prelude.RightAsync<Error, NetworkProvidersConfiguration>(
                networkProvidersConfig));

        await WithScope(async (_, stateStore) =>
        {
            var configRealizer = new NetworkProvidersConfigRealizer(stateStore);
            await configRealizer.RealizeConfigAsync(networkProvidersConfig, default);
        });
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = Guid.Parse(CatletMetadataId),
        });

        await stateStore.For<Project>().AddAsync(new Project()
        {
            Id = Guid.Parse(SecondProjectId),
            Name = "second-project",
            TenantId = EryphConstants.DefaultTenantId,
        });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(DefaultNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.0.0.0/15",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(DefaultSubnetId),
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
                        ],
                    },
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondSubnetId),
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
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "42:00:42:00:00:01",
                    }
                ],
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = "second-network",
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.5.0.0/16",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondNetworkSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.5.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.5.0.0/16",
                                FirstIp = "10.5.0.10",
                                NextIp = "10.5.0.12",
                                LastIp = "10.5.0.19",
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = "second-provider-pool",
                        MacAddress = "42:00:42:00:00:02",
                    }
                ]
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(ThirdNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = "third-network",
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.6.0.0/16",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(ThirdNetworkSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.6.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.6.0.0/16",
                                FirstIp = "10.6.0.10",
                                NextIp = "10.6.0.12",
                                LastIp = "10.6.0.19",
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = "second-provider-subnet",
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "42:00:42:00:00:03",
                    }
                ]
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondEnvironmentNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultNetworkName,
                Environment = "second-environment",
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.10.0.0/16",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondEnvironmentSubnetId),
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
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "42:00:42:00:00:04",
                    }
                ]
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.NewGuid(),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = "second-provider",
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = "second-overlay-provider",
                IpNetwork = "10.200.0.0/16",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.NewGuid(),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.200.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.200.0.0/16",
                                FirstIp = "10.200.0.10",
                                NextIp = "10.200.0.12",
                                LastIp = "10.200.0.19",
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = "second-overlay-provider",
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "42:00:42:00:00:06",
                    }
                ]
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(SecondProjectNetworkId),
                ProjectId = Guid.Parse(SecondProjectId),
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.100.0.0/16",
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(SecondProjectSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.100.0.0/16",
                        IpPools =
                        [
                            new IpPool()
                            {
                                Id = Guid.NewGuid(),
                                Name = EryphConstants.DefaultIpPoolName,
                                IpNetwork = "10.100.0.0/16",
                                FirstIp = "10.100.0.10",
                                NextIp = "10.100.0.12",
                                LastIp = "10.100.0.19",
                            }
                        ],
                    },
                ],
                NetworkPorts =
                [
                    new ProviderRouterPort
                    {
                        Name = "provider",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "42:00:42:00:00:05",
                    }
                ]
            });

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(FlatNetworkId),
                ProjectId = Guid.Parse(DefaultProjectId),
                Name = "flat-network",
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = "flat-provider",
            });
    }
}