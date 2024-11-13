using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.TestBase;
using Moq;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Tests.Networks;

public class UpdateCatletNetworksCommandHandlerTests : InMemoryStateDbTestBase
{
    private const string DefaultProjectId = "4b4a3fcf-b5ed-4a9a-ab6e-03852752095e";
    private const string SecondProjectId = "75c27daf-77c8-4b98-a072-a4706dceb422";

    private const string DefaultNetworkId = "cb58fe00-3f64-4b66-b58e-23fb15df3cac";
    private const string DefaultSubnetId = "ed6697cd-836f-4da7-914b-b09ed1567934";
    private const string SecondSubnetId = "4f976208-613a-40d4-a284-d32cbd4a1b8e";

    private const string SecondNetworkId = "e480a020-57d0-4443-a973-57aa0c95872e";
    private const string SecondNetworkSubnetId = "27ec11a4-5d6a-47da-9f9f-eb7486db38ea";

    private const string SecondEnvironmentNetworkId = "81a139e5-ab61-4fe3-b81f-59c11a665d22";
    private const string SecondEnvironmentSubnetId = "dc807357-50e7-4263-8298-0c97ff69f4cf";

    private const string SecondProjectNetworkId = "c0043e88-8268-4ac0-b027-2fa37ad3168f";
    private const string SecondProjectSubnetId = "0c721846-5e2e-40a9-83d2-f1b75206ef84";

    private const string FlatNetworkId = "98ff838a-a2c3-464d-8884-f348888ed804";

    private const string CatletMetadataId = "15e2b061-c625-4469-9fe7-7c455058fcc0";
    private const string CatletId = "de8c6710-172a-44be-bbed-27ba9905ed8f";

    private readonly Mock<ITaskMessaging> _taskMessingMock = new();
    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();



    [Fact]
    public async Task UpdateNetworks_CatletIsAddedToOverlayNetwork_CreatesCorrectNetworkConfig()
    {
        await ArrangeCatlet(DefaultProjectId);

        var command = new UpdateCatletNetworksCommand
        {
            
            CatletId = Guid.Parse(CatletId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Networks = 
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                    }
                ]
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(command);

            result.Should().BeRight().Which.Should().SatisfyRespectively(
                settings =>
                {
                    settings.NetworkProviderName.Should().Be("default");
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"{CatletId}_eth0");
                });

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var assignmentCount = await stateStore.For<IpAssignment>().CountAsync();
            assignmentCount.Should().Be(2);
        });
    }

    // TODO Verify flat network creates a port
    // TODO Verify that the floating port and IP assignment were removed

    [Fact]
    public async Task UpdateNetworks_SwitchFromFlatToOverlay_CreatesCorrectNetworkConfig()
    {
        // TODO Verify that the floating port and IP assignment are created
    }

    [Fact]
    public async Task UpdateNetworks_RemoveNetwork_CreatesCorrectNetworkConfig()
    {
        // TODO Verify that the port and assignment were deleted
    }

    // TODO test change of project
    // TODO test change of environment

    private async Task WithScope(Func<UpdateCatletNetworksCommandHandler, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var handler = scope.GetInstance<UpdateCatletNetworksCommandHandler>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(handler, stateStore);
    }

    private async Task ArrangeCatlet(string projectId)
    {
        await WithScope(async (_, stateStore) =>
        {
            await stateStore.For<Catlet>().AddAsync(new Catlet
            {
                Id = Guid.Parse(CatletId),
                ProjectId = Guid.Parse(projectId),
                MetadataId = Guid.Parse(CatletMetadataId),
                Name = "test-catlet",
                Environment = "default",
                DataStore = "default",
            });

            await stateStore.SaveChangesAsync();
        });
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.RegisterInstance(_taskMessingMock.Object);
        options.Container.RegisterInstance(_networkProviderManagerMock.Object);

        // Use the proper managers instead of mocks as the code quite
        // interdependent as it modifies the same EF Core entities.
        options.Container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
        options.Container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
        options.Container.Register<IProviderIpManager, ProviderIpManager>(Lifestyle.Scoped);

        options.Container.Register<UpdateCatletNetworksCommandHandler>(Lifestyle.Scoped);
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

        var projectB = new Project()
        {
            Id = Guid.Parse(SecondProjectId),
            Name = "second-project",
            TenantId = EryphConstants.DefaultTenantId,
        };
        await stateStore.For<Project>().AddAsync(projectB);

        await stateStore.For<VirtualNetwork>().AddAsync(
            new VirtualNetwork
            {
                Id = Guid.Parse(DefaultNetworkId),
                ProjectId = EryphConstants.DefaultProjectId,
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
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
                        Name = "default",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = "second-provider-subnet",
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "00:00:00:01:00:01",
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
                        Name = "default",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = "second-provider-subnet",
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "00:00:00:01:00:02",
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
                        Name = "default",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "00:00:00:01:00:03",
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
                        Name = "default",
                        ProviderName = EryphConstants.DefaultProviderName,
                        SubnetName = EryphConstants.DefaultSubnetName,
                        PoolName = EryphConstants.DefaultIpPoolName,
                        MacAddress = "00:00:00:01:00:04",
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
