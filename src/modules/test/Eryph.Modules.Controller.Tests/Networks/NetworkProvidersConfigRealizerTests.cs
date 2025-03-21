using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector.Integration.ServiceCollection;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks;

public class NetworkProvidersConfigRealizerTests(
    ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private readonly NetworkProvidersConfiguration _complexConfig = new()
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

    private readonly NetworkProvidersConfiguration _simpleConfig = new()
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
                        ],
                    },
                ],
            },
        ],
    };

    [Fact]
    public async Task RealizeConfigAsync_NoExistingSubnets_CreatesCorrectSubnets()
    {
        await WithScope(async (realizer, _) =>
        {
            await realizer.RealizeConfigAsync(_complexConfig, default);
        });

        await WithScope(async (_, stateStore) =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(new GetAllSubnets());
            subnets.Should().HaveCount(2);

            {
                var subnet = subnets.Should().ContainSingle(s => s.Name == "default").Subject;

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
            await realizer.RealizeConfigAsync(_complexConfig, default);
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
            await realizer.RealizeConfigAsync(_simpleConfig, default);
        });

        await WithScope(async (_, stateStore) =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(new GetAllSubnets());
            subnets.Should().SatisfyRespectively(
                subnet =>
                {
                    subnet.Name.Should().Be("default");
                    subnet.IpNetwork.Should().Be("10.249.248.0/24");

                    subnet.IpPools.Should().SatisfyRespectively(
                        pool =>
                        {
                            pool.Name.Should().Be("default");
                            pool.FirstIp.Should().Be("10.249.248.10");
                            pool.NextIp.Should().Be("10.249.248.12");
                            pool.LastIp.Should().Be("10.249.248.19");
                        });
                });
        });
    }

    [Fact]
    public async Task RealizeConfigAsync_IpRangeOfExistingPoolIsChanged_ExistingPoolIsUpdated()
    {
        await WithScope(async (realizer, _) =>
        {
            await realizer.RealizeConfigAsync(_simpleConfig, default);
        });

        Guid ipPoolId = Guid.Empty;
        await WithScope(async (_, stateStore) =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(new GetAllSubnets());
            subnets.Should().SatisfyRespectively(
                subnet =>
                {
                    subnet.Name.Should().Be("default");
                    subnet.IpNetwork.Should().Be("10.249.248.0/24");

                    subnet.IpPools.Should().SatisfyRespectively(
                        pool =>
                        {
                            pool.Name.Should().Be("default");
                            pool.FirstIp.Should().Be("10.249.248.10");
                            pool.NextIp.Should().Be("10.249.248.12");
                            pool.LastIp.Should().Be("10.249.248.19");
                        });
                });
            ipPoolId = subnets.Should().ContainSingle()
                .Which.IpPools.Should().ContainSingle()
                .Which.Id;
        });

        await WithScope(async (realizer, _) =>
        {
            _simpleConfig.NetworkProviders[0].Subnets[0].IpPools[0].LastIp = "10.249.248.100";
            await realizer.RealizeConfigAsync(_simpleConfig, default);
        });

        await WithScope(async (_, stateStore) =>
        {
            var subnets = await stateStore.For<ProviderSubnet>().ListAsync(new GetAllSubnets());
            subnets.Should().SatisfyRespectively(
                subnet =>
                {
                    subnet.Name.Should().Be("default");
                    subnet.IpNetwork.Should().Be("10.249.248.0/24");

                    subnet.IpPools.Should().SatisfyRespectively(
                        pool =>
                        {
                            pool.Id.Should().Be(ipPoolId);
                            pool.Name.Should().Be("default");
                            pool.FirstIp.Should().Be("10.249.248.10");
                            pool.NextIp.Should().Be("10.249.248.12");
                            pool.LastIp.Should().Be("10.249.248.100");
                        });
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

    private sealed class GetAllSubnets : Specification<ProviderSubnet>
    {
        public GetAllSubnets()
        {
            Query.Include(x => x.IpPools);
        }
    }
}
