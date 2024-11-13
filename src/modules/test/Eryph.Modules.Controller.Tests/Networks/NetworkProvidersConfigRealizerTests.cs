using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller.Tests.Networks;

public class NetworkProvidersConfigRealizerTests : InMemoryStateDbTestBase
{
    private readonly NetworkProvidersConfiguration _config = new()
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

    [Fact]
    public async Task RealizeConfigAsync_NoExistingSubnets_CreatesCorrectSubnets()
    {
        await WithScope(async (realizer, _) =>
        {
            await realizer.RealizeConfigAsync(_config, default);
        });

        await WithScope(async (_, stateStore) =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync();
            subnets.Should().HaveCount(2);

            {
                var subnet = subnets.Should().ContainSingle(s => s.Name == "default").Subject;
                await stateStore.LoadCollectionAsync(subnet, s => s.IpPools);

                subnet.IpNetwork.Should().Be("10.249.248.0/24");
                subnet.IpPools.Should().HaveCount(2);

                var defaultPool = subnet.IpPools.Should().ContainSingle(p => p.Name == "default").Subject;
                defaultPool.FirstIp.Should().Be("10.249.248.10");
                defaultPool.NextIp.Should().Be("10.249.248.12");
                defaultPool.LastIp.Should().Be("10.249.248.19");

                var secondPool = subnet.IpPools.Should().ContainSingle(p => p.Name == "second-provider-pool").Subject;
                secondPool.FirstIp.Should().Be("10.249.248.20");
                secondPool.NextIp.Should().Be("10.249.248.22");
                secondPool.LastIp.Should().Be("10.249.248.29");
            }

            {
                var subnet = subnets.Should().ContainSingle(s => s.Name == "second-provider-subnet").Subject;
                await stateStore.LoadCollectionAsync(subnet, s => s.IpPools);

                subnet.IpNetwork.Should().Be("10.249.249.0/24");
                subnet.IpPools.Should().HaveCount(1);

                var defaultPool = subnet.IpPools.Should().ContainSingle(p => p.Name == "default").Subject;
                defaultPool.FirstIp.Should().Be("10.249.249.10");
                defaultPool.NextIp.Should().Be("10.249.249.12");
                defaultPool.LastIp.Should().Be("10.249.249.19");
            }
        });
    }

    [Fact]
    public async Task RealizeConfigAsync_ExistingSubnets_RemovesOldSubnets()
    {
        await WithScope(async (realizer, _) =>
        {
            await realizer.RealizeConfigAsync(_config, default);
        });

        await WithScope(async (_, stateStore) =>
        {
            var subnetCount = await stateStore.For<ProviderSubnet>().CountAsync();
            subnetCount.Should().Be(2);

            var poolCount = await stateStore.For<IpPool>().CountAsync();
            poolCount.Should().Be(3);
        });

        await WithScope(async (realizer, _) =>
        {
            var updatedConfig = new NetworkProvidersConfiguration
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
                                ],
                            },
                        ],
                    },
                ],
            };
            await realizer.RealizeConfigAsync(updatedConfig, default);

            await WithScope(async (_, stateStore) =>
            {
                var subnets = await stateStore.For<ProviderSubnet>().ListAsync();
                subnets.Should().HaveCount(1);

                var subnet = subnets.Should().ContainSingle(s => s.Name == "default").Subject;
                await stateStore.LoadCollectionAsync(subnet, s => s.IpPools);
                
                subnet.IpNetwork.Should().Be("10.249.248.0/24");
                subnet.IpPools.Should().HaveCount(1);

                var defaultPool = subnet.IpPools.Should().ContainSingle(p => p.Name == "default").Subject;
                defaultPool.FirstIp.Should().Be("10.249.248.10");
                defaultPool.NextIp.Should().Be("10.249.248.12");
                defaultPool.LastIp.Should().Be("10.249.248.19");
                
            });
        });
    }

    private async Task WithScope(Func<INetworkProvidersConfigRealizer, IStateStore, Task> func)
    {
        await using var scope = CreateScope();
        var catletIpManager = scope.GetInstance<INetworkProvidersConfigRealizer>();
        var stateStore = scope.GetInstance<IStateStore>();
        await func(catletIpManager, stateStore);
    }

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.Container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>();
    }
}
