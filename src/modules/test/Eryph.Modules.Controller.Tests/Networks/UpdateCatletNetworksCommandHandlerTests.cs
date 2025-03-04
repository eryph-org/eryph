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
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks;

public class UpdateCatletNetworksCommandHandlerTests(
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
    private const string CatletId = "de8c6710-172a-44be-bbed-27ba9905ed8f";

    private readonly Mock<ITaskMessaging> _taskMessingMock = new();
    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();

    [Theory]
    [InlineData(DefaultProjectId, "default", null, null, null,
        DefaultNetworkId, "default", "default", "10.0.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "default", "default", "default",
        DefaultNetworkId, "default", "default", "10.0.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "default", "default", "second-pool",
        DefaultNetworkId, "default", "default", "10.0.1.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "default", "second-subnet", "default",
        DefaultNetworkId, "default", "default", "10.1.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "second-network", "default", "default",
        SecondNetworkId, "default", "second-provider-pool", "10.5.0.12", "10.249.248.22")]
    [InlineData(DefaultProjectId, "default", "third-network", "default", "default",
        ThirdNetworkId, "second-provider-subnet", "default", "10.6.0.12", "10.249.249.12")]
    [InlineData(DefaultProjectId, "second-environment", "default", "default", "default",
        SecondEnvironmentNetworkId, "default", "default", "10.10.0.12", "10.249.248.12")]
    // When the environment does not have a dedicated network, we should fall back to the default network
    [InlineData(DefaultProjectId, "environment-without-network", "default", "default", "default",
        DefaultNetworkId, "default", "default", "10.0.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "default", "default", "second-pool",
        DefaultNetworkId, "default", "default", "10.0.1.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "default", "second-subnet", "default",
        DefaultNetworkId, "default", "default", "10.1.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "second-network", "default", "default",
        SecondNetworkId, "default", "second-provider-pool", "10.5.0.12", "10.249.248.22")]
    [InlineData(DefaultProjectId, "environment-without-network", "third-network", "default", "default",
        ThirdNetworkId, "second-provider-subnet", "default", "10.6.0.12", "10.249.249.12")]
    [InlineData(SecondProjectId, "default", "default", "default", "default",
        SecondProjectNetworkId, "default", "default", "10.100.0.12", "10.249.248.12")]
    public async Task UpdateNetworks_CatletIsAddedToOverlayNetwork_CreatesCorrectNetworkConfig(
        string projectId,
        string environment,
        string? networkName,
        string? subnetName,
        string? poolName,
        string expectedNetworkId,
        string expectedProviderSubnet,
        string expectedProviderPool,
        string expectedIp,
        string expectedFloatingIp)
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(projectId),
            Config = new CatletConfig
            {
                Environment = environment,
                Networks = 
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = networkName,
                        SubnetV4 = subnetName is not null || poolName is not null
                            ? new CatletSubnetConfig
                            {
                                Name = subnetName,
                                IpPool = poolName,
                            }
                            : null,
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
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.NetworkName.Should().Be(networkName);
                    settings.AddressesV4.Should().Equal(expectedIp);
                    settings.FloatingAddressV4.Should().Be(expectedFloatingIp);
                    settings.AddressesV6.Should().BeEmpty();
                    settings.FloatingAddressV6.Should().BeNull();
                });

            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            expectedNetworkId,
            expectedProviderSubnet, expectedProviderPool,
            expectedIp, expectedFloatingIp);
    }

    [Fact]
    public async Task UpdateNetworks_CatletIsAddedToFlatNetwork_CreatesCorrectNetworkConfig()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Environment = "default",
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = "flat-network",
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
                    settings.NetworkProviderName.Should().Be("flat-provider");
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.NetworkName.Should().Be("flat-network");
                    settings.AddressesV4.Should().BeEmpty();
                    settings.FloatingAddressV4.Should().BeNull();
                    settings.AddressesV6.Should().BeEmpty();
                    settings.FloatingAddressV6.Should().BeNull();
                });

            await stateStore.SaveChangesAsync();
        });

        await ShouldBeFlatNetworkInDatabase();
    }

    [Fact]
    public async Task UpdateNetworks_CatletHasFixedMacAddress_FixedMacAddressIsUsed()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                NetworkAdapters =
                [
                    new CatletNetworkAdapterConfig
                    {
                        Name = "eth0",
                        MacAddress = "420042004202",
                    }
                ],
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = "flat-network",
                    }
                ],
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(command);

            result.Should().BeRight().Which.Should().SatisfyRespectively(
                settings =>
                {
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.MacAddress.Should().Be("42:00:42:00:42:02");
                });

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync();
            var catletPort = catletPorts.Should().ContainSingle().Subject;
            catletPort.CatletMetadataId.Should().Be(Guid.Parse(CatletMetadataId));
            catletPort.MacAddress.Should().Be("42:00:42:00:42:02");
        });
    }

    [Fact]
    public async Task UpdateNetworks_CatletHasHostName_HostnameIsUsed()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Hostname = "test-catlet",
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                    }
                ],
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(command);
            result.Should().BeRight();

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync();
            var catletPort = catletPorts.Should().ContainSingle().Subject;
            catletPort.CatletMetadataId.Should().Be(Guid.Parse(CatletMetadataId));
            catletPort.AddressName.Should().Be("test-catlet");
        });
    }

    [Fact]
    public async Task UpdateNetworks_MoveCatletFromFlatNetworkToOverlayNetwork_CreatesCorrectNetworkConfig()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Environment = "default",
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = "flat-network",
                    }
                ]
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(command);
            result.Should().BeRight();
            await stateStore.SaveChangesAsync();
        });

        await ShouldBeFlatNetworkInDatabase();

        var updatedConfigCommand = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Environment = "default",
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = "default",
                        SubnetV4 =  new CatletSubnetConfig
                        {
                            Name = "default",
                            IpPool = "default",
                        },
                    }
                ]
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(updatedConfigCommand);
            result.Should().BeRight().Which.Should().SatisfyRespectively(
                settings =>
                {
                    settings.NetworkProviderName.Should().Be("default");
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.NetworkName.Should().Be("default");
                    settings.AddressesV4.Should().Equal("10.0.0.12");
                    settings.FloatingAddressV4.Should().Be("10.249.248.12");
                    settings.AddressesV6.Should().BeEmpty();
                    settings.FloatingAddressV6.Should().BeNull();
                });

            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            DefaultNetworkId,
            "default", "default",
            "10.0.0.12", "10.249.248.12");
    }

    [Fact]
    public async Task UpdateNetworks_MoveCatletFromOverlayNetworkToFlatNetwork_CreatesCorrectNetworkConfig()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
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
            result.Should().BeRight();
            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            DefaultNetworkId,
            "default", "default",
            "10.0.0.12", "10.249.248.12");

        var updatedConfigCommand = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Environment = "default",
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = "flat-network",
                    }
                ]
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(updatedConfigCommand);
            result.Should().BeRight().Which.Should().SatisfyRespectively(
                settings =>
                {
                    settings.NetworkProviderName.Should().Be("flat-provider");
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.NetworkName.Should().Be("flat-network");
                    settings.AddressesV4.Should().BeEmpty();
                    settings.FloatingAddressV4.Should().BeNull();
                    settings.AddressesV6.Should().BeEmpty();
                    settings.FloatingAddressV6.Should().BeNull();
                });

            await stateStore.SaveChangesAsync();
        });

        await ShouldBeFlatNetworkInDatabase();
    }

    [Theory]
    [InlineData(DefaultProjectId, "default", "default", "default", "second-pool",
        DefaultNetworkId, "default", "default", "10.0.1.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "default", "second-subnet", "default",
        DefaultNetworkId, "default", "default", "10.1.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "default", "second-network", "default", "default",
        SecondNetworkId, "default", "second-provider-pool", "10.5.0.12", "10.249.248.22")]
    [InlineData(DefaultProjectId, "default", "third-network", "default", "default",
        ThirdNetworkId, "second-provider-subnet", "default", "10.6.0.12", "10.249.249.12")]
    [InlineData(DefaultProjectId, "second-environment", "default", "default", "default",
        SecondEnvironmentNetworkId, "default", "default", "10.10.0.12", "10.249.248.12")]
    // When the environment does not have a dedicated network, we should fall back to the default network
    [InlineData(DefaultProjectId, "environment-without-network", "default", "default", "second-pool",
        DefaultNetworkId, "default", "default", "10.0.1.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "default", "second-subnet", "default",
        DefaultNetworkId, "default", "default", "10.1.0.12", "10.249.248.12")]
    [InlineData(DefaultProjectId, "environment-without-network", "second-network", "default", "default",
        SecondNetworkId, "default", "second-provider-pool", "10.5.0.12", "10.249.248.22")]
    [InlineData(DefaultProjectId, "environment-without-network", "third-network", "default", "default",
        ThirdNetworkId, "second-provider-subnet", "default", "10.6.0.12", "10.249.249.12")]
    [InlineData(SecondProjectId, "default", "default", "default", "default",
        SecondProjectNetworkId, "default", "default", "10.100.0.12", "10.249.248.12")]
    public async Task UpdateNetworks_CatletIsMovedBetweenOverlayNetworks_CreatesCorrectNetworkConfig(
        string projectId,
        string environment,
        string? networkName,
        string? subnetName,
        string? poolName,
        string expectedNetworkId,
        string expectedProviderSubnet,
        string expectedProviderPool,
        string expectedIp,
        string expectedFloatingIp)
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
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
            result.Should().BeRight();
            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            DefaultNetworkId,
            "default", "default",
            "10.0.0.12", "10.249.248.12");

        var updatedConfigCommand = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(projectId),
            Config = new CatletConfig
            {
                Environment = environment,
                Networks =
                [
                    new CatletNetworkConfig
                    {
                        AdapterName = "eth0",
                        Name = networkName,
                        SubnetV4 = new CatletSubnetConfig
                        {
                            Name = subnetName,
                            IpPool = poolName,
                        }
                    }
                ]
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(updatedConfigCommand);
            result.Should().BeRight().Which.Should().SatisfyRespectively(
                settings =>
                {
                    settings.NetworkProviderName.Should().Be("default");
                    settings.AdapterName.Should().Be("eth0");
                    settings.PortName.Should().Be($"ovs_{CatletId}_eth0");
                    settings.NetworkName.Should().Be(networkName);
                    settings.AddressesV4.Should().Equal(expectedIp);
                    settings.FloatingAddressV4.Should().Be(expectedFloatingIp);
                    settings.AddressesV6.Should().BeEmpty();
                    settings.FloatingAddressV6.Should().BeNull();
                });

            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            expectedNetworkId,
            expectedProviderSubnet, expectedProviderPool,
            expectedIp, expectedFloatingIp);
    }

    [Fact]
    public async Task UpdateNetworks_NetworkIsRemovedFromCatlet_PortsAreDeleted()
    {
        var command = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
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
            result.Should().BeRight();
            await stateStore.SaveChangesAsync();
        });

        await ShouldBeOverlayNetworkInDatabase(
            DefaultNetworkId,
            "default", "default",
            "10.0.0.12", "10.249.248.12");

        var updatedConfigCommand = new UpdateCatletNetworksCommand
        {
            CatletId = Guid.Parse(CatletId),
            CatletMetadataId = Guid.Parse(CatletMetadataId),
            ProjectId = Guid.Parse(DefaultProjectId),
            Config = new CatletConfig
            {
                Networks = [],
            },
        };

        await WithScope(async (handler, stateStore) =>
        {
            var result = await handler.UpdateNetworks(updatedConfigCommand);
            result.Should().BeRight().Which.Should().BeEmpty();

            await stateStore.SaveChangesAsync();
        });

        await WithScope(async (_, stateStore) =>
        {
            var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync();
            catletPorts.Should().BeEmpty();

            var floatingPorts = await stateStore.For<FloatingNetworkPort>().ListAsync();
            floatingPorts.Should().BeEmpty();

            var assignments = await stateStore.For<IpAssignment>().ListAsync();
            assignments.Should().BeEmpty();
        });
    }

    private async Task ShouldBeOverlayNetworkInDatabase(
        string expectedNetworkId,
        string expectedProviderSubnet,
        string expectedProviderPool,
        string expectedIp,
        string expectedFloatingIp)
    {
        await WithScope(async (_, stateStore) =>
        {
            var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync();
            var catletPort = catletPorts.Should().ContainSingle().Subject;
            catletPort.CatletMetadataId.Should().Be(Guid.Parse(CatletMetadataId));
            catletPort.NetworkId.Should().Be(Guid.Parse(expectedNetworkId));

            var floatingPorts = await stateStore.For<FloatingNetworkPort>().ListAsync();
            var floatingPort = floatingPorts.Should().ContainSingle().Subject;
            floatingPort.ProviderName.Should().Be("default");
            floatingPort.SubnetName.Should().Be(expectedProviderSubnet);
            floatingPort.PoolName.Should().Be(expectedProviderPool);

            var assignments = await stateStore.For<IpAssignment>().ListAsync();
            assignments.Should().Satisfy(
                assignment => assignment.IpAddress == expectedIp,
                assignment => assignment.IpAddress == expectedFloatingIp);
        });
    }

    private async Task ShouldBeFlatNetworkInDatabase()
    {
        await WithScope(async (_, stateStore) =>
        {
            var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync();
            var catletPort = catletPorts.Should().ContainSingle().Subject;
            catletPort.CatletMetadataId.Should().Be(Guid.Parse(CatletMetadataId));
            catletPort.NetworkId.Should().Be(Guid.Parse(FlatNetworkId));

            var floatingPorts = await stateStore.For<FloatingNetworkPort>().ListAsync();
            floatingPorts.Should().BeEmpty();

            var assignments = await stateStore.For<IpAssignment>().ListAsync();
            assignments.Should().BeEmpty();
        });
    }

    private async Task WithScope(Func<UpdateCatletNetworksCommandHandler, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var handler = scope.GetInstance<UpdateCatletNetworksCommandHandler>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(handler, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.RegisterInstance(_taskMessingMock.Object);
        options.Container.RegisterInstance(_networkProviderManagerMock.Object);

        // Use the proper managers instead of mocks. The code is quite
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
                    Type = NetworkProviderType.NatOverlay,
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
                    Type = NetworkProviderType.Flat,
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
                Subnets =
                [
                    new VirtualNetworkSubnet
                    {
                        Id = Guid.Parse(DefaultSubnetId),
                        Name = EryphConstants.DefaultSubnetName,
                        IpNetwork = "10.0.0.0/15",
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
                        IpNetwork = "10.5.0.0/16",
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
                Id = Guid.Parse(SecondProjectNetworkId),
                ProjectId = Guid.Parse(SecondProjectId),
                Name = EryphConstants.DefaultNetworkName,
                Environment = EryphConstants.DefaultEnvironmentName,
                NetworkProvider = EryphConstants.DefaultProviderName,
                IpNetwork = "10.200.0.0/16",
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
