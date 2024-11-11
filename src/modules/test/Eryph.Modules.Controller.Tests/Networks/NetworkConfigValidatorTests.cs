using Microsoft.Data.Sqlite;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Sqlite;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests.Networks
{
    public sealed class NetworkConfigValidatorTests : IDisposable, IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;

        private readonly SqliteConnection _connection;

        public NetworkConfigValidatorTests(ITestOutputHelper output)
        {
            _output = output;
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
        }

        [Fact]
        public async Task Can_change_unused_network()
        {
            _projectConfig.Networks.Find(x => x.Name == "unused_network").IfSome(c => c.Address = "192.168.12.0/24");
                var messages = await RunChangeValidator(_projectConfig);

            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_remove_used_network()
        {
            _projectConfig.Networks = _projectConfig.Networks!.Where(x => x.Name != "used_network").ToArray();
            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "environment 'default', network 'used_network': Network is in use (1 ports) - cannot remove network.");
        }

        [Fact]
        public async Task Change_validator_reports_missing_provider()
        {
            _projectConfig.Networks.Find(x => x.Name == "unused_network").IfSome(c => c.Provider = new ProviderConfig
            {
                Name = "missing_provider"
            });
            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "environment 'default', network 'unused_network': could not find network provider 'missing_provider'");
        }

        [Fact]
        public async Task Cannot_change_provider_port_of_used_network()
        {
            _projectConfig.Networks.Find(x => x.Name == "used_network").IfSome(c => c.Provider = new ProviderConfig
            {
                Name = "default",
                Subnet = "new_subnet",
                IpPool = "new_pool"
            }); var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "network 'used_network': Network is in use (1 ports) -  changing network provider is not supported.'")
                .And.Contain("network 'used_network': To change the network first remove all ports or move them to another network.");
        }

        [Fact]
        public async Task Cannot_change_subnet_of_used_network()
        {
            _projectConfig.Networks.Find(x => x.Name == "used_network").IfSome(c => c.Subnets = new []
            {
                new NetworkSubnetConfig
                {
                    Address = "192.168.15.0/24"
                }
            }); var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "network 'used_network': Network is in use (1 ports) -  changing addresses is not supported'")
                .And.Contain("network 'used_network': To change the network first remove all ports or move them to another network.");
        }

        [Fact]
        public async Task Cannot_delete_used_ip_pool()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools = s.IpPools!.Where(p => p.Name != "pool2").ToArray()));
            
            
            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2': Cannot delete a used ip pool (1 ip assignments found) .");
        }

        [Fact]
        public async Task Can_delete_unused_ip_pool()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools = s.IpPools!.Where(p => p.Name != "pool1").ToArray()));


            var messages = await RunChangeValidator(_projectConfig);

            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_change_used_ip_pool_start()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools!.Find(p => p.Name == "pool2")
                        .IfSome(pool => pool.FirstIp = "192.168.15.150" )));


            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2': Changing the first ip of a used ip pool is not supported.");
        }

        [Fact]
        public async Task Can_change_used_ip_pool_end_up_to_used()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools!.Find(p => p.Name == "pool2")
                        .IfSome(pool => pool.LastIp = "192.168.15.110")));


            var messages = await RunChangeValidator(_projectConfig);

            messages.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_change_used_ip_pool_end_below_used()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools!.Find(p => p.Name == "pool2")
                        .IfSome(pool => pool.LastIp = "192.168.15.109")));


            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2': Cannot change last ip to '192.168.15.109' as there are already higher addresses assigned (e.g.: '192.168.15.110').");
        }

        [Fact]
        public async Task Cannot_change_used_ip_pool_next_to_already_assigned()
        {
            _projectConfig.Networks.Find(x => x.Name == "pool_network")
                .IfSome(n => n.Subnets.Find(subnet => subnet.Name == "default")
                    .IfSome(s => s.IpPools!.Find(p => p.Name == "pool2")
                        .IfSome(pool => pool.NextIp = "192.168.15.110")));


            var messages = await RunChangeValidator(_projectConfig);

            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2': Cannot change next ip to '192.168.15.110' as this ip is already assigned.");
        }

        [Fact]
        public void Config_requires_network_name()
        {
            _projectConfig.Networks![0].Name = null;
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("Empty network name");

        }

        [Fact]
        public void Config_reports_invalid_network_provider()
        {
            _projectConfig.Networks![0].Provider = new ProviderConfig { Name = "invalid" };
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("network 'unused_network': could not find network provider 'invalid'");

        }

        [Fact]
        public void Config_reports_invalid_network_provider_subnet()
        {
            _projectConfig.Networks![0].Provider = new ProviderConfig { Name = "default", Subnet = "invalid" };
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("network 'unused_network': provider subnet 'default/invalid' not found");
        }


        [Fact]
        public void Config_reports_invalid_network_provider_ip_pool()
        {
            _projectConfig.Networks![0].Provider = new ProviderConfig { Name = "default", IpPool = "invalid" };
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("network 'unused_network': provider ip pool 'default/default/invalid' not found");
        }

        [Fact]
        public void Config_reports_invalid_network_address()
        {
            _projectConfig.Networks![0].Address = "192.168.500.0/24";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("network 'unused_network': Invalid network address '192.168.500.0/24'");
        }

        [Fact]
        public void Config_reports_use_of_ipv6()
        {
            _projectConfig.Networks![0].Address = "2607:f0d0:1002:51::4";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(2);
            messages.Should().Contain("network 'unused_network': network address '2607:f0d0:1002:51::4' is not a IPV4 address'");
        }

        [Fact]
        public void Config_reports_network_cidr_mismatch()
        {
            _projectConfig.Networks![0].Address = "192.168.10.0/8";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().Contain("network 'unused_network': Invalid network address '192.168.10.0/8' - network cidr match network '192.0.0.0/8'");
        }

        [Fact]
        public void Config_reports_invalid_subnet_address()
        {
            _projectConfig.Networks![0].Subnets![0].Address = "192.168.500.0/24";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("subnet 'unused_network/default': Invalid network address '192.168.500.0/24'");
        }

        [Fact]
        public void Config_reports_invalid_subnet_address_mismatch()
        {
            _projectConfig.Networks![0].Subnets![0].Address = "192.168.20.0/24";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("subnet 'unused_network/default': network address '192.168.20.0/24' is not a subnet of '192.168.10.0/24'");
        }

        [Fact]
        public void Config_reports_subnet_cidr_mismatch()
        {
            _projectConfig.Networks![0].Subnets![0].Address = "192.168.10.1/24";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().Contain("subnet 'unused_network/default': Invalid network address '192.168.10.1/24' - network cidr match network '192.168.10.0/24'");
        }

        [Fact]
        public void Config_reports_invalid_pool_ips()
        {
            _projectConfig.Networks![2].Subnets![0].IpPools![1].FirstIp = "192.168.15.1/24";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].NextIp = "192.168.15.5/24";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].LastIp = "192.168.15.10/24";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().Contain("ip pool 'pool_network/default/pool2': Invalid ip address '192.168.15.1/24'")
                .And.Contain("ip pool 'pool_network/default/pool2': Invalid ip address '192.168.15.5/24'")
                .And.Contain("ip pool 'pool_network/default/pool2': Invalid ip address '192.168.15.10/24'");
        }

        [Fact]
        public void Config_reports_pool_ips_mismatch()
        {
            _projectConfig.Networks![2].Subnets![0].IpPools![1].FirstIp = "192.168.1.1";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].NextIp = "192.168.2.1";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].LastIp = "192.168.3.1";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should().Contain("ip pool 'pool_network/default/pool2': ip address '192.168.1.1' is not in subnet '192.168.15.0/24'")
                .And.Contain("ip pool 'pool_network/default/pool2': ip address '192.168.2.1' is not in subnet '192.168.15.0/24'")
                .And.Contain("ip pool 'pool_network/default/pool2': ip address '192.168.3.1' is not in subnet '192.168.15.0/24'");
        }

        [Fact]
        public void Config_reports_pool_ips_last_larger_than_first()
        {
            _projectConfig.Networks![2].Subnets![0].IpPools![1].FirstIp = "192.168.15.200";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].LastIp = "192.168.15.100";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2':last ip address '192.168.15.100' is not larger then first ip address '192.168.15.200'");
        }

        [Fact]
        public void Config_reports_pool_ips_next_larger_than_last()
        {
            _projectConfig.Networks![2].Subnets![0].IpPools![1].NextIp = "192.168.15.200";
            _projectConfig.Networks![2].Subnets![0].IpPools![1].LastIp = "192.168.15.150";
            var messages = RunConfigValidator(_projectConfig);
            messages.Should()
                .Contain(
                    "ip pool 'pool_network/default/pool2': Next ip address '192.168.15.200' is invalid as it is higher than last ip address '192.168.15.150'");
        }

        [Fact]
        public void Config_reports_invalid_subnet_config_on_flat_provider()
        {
            _projectConfig.Networks![0].Provider = new ProviderConfig
            {
                Name = "flat",
                Subnet = "subnet"
            };

            var messages = RunConfigValidator(_projectConfig);
            messages.Should()
                .Contain(
                    "network 'unused_network': provider subnets and ip pools are not supported for flat networks.");
        }


        [Fact]
        public void Config_allows_same_network_name_on_different_environments()
        {
            _projectConfig.Networks![0].Environment = "new";
            _projectConfig.Networks![0].Name = "default";
            _projectConfig.Networks![1].Name = "default";

            var messages = RunConfigValidator(_projectConfig);
            messages.Should().BeEmpty();
        }

        [Fact]
        public void Config_reports_same_network_name_on_same_environments()
        {
            _projectConfig.Networks![0].Environment = "new";
            _projectConfig.Networks![0].Name = "default";
            _projectConfig.Networks![1].Name = "default";
            _projectConfig.Networks![1].Environment = "new";

            var messages = RunConfigValidator(_projectConfig);
            messages.Should().HaveCount(1);
            messages.Should().Contain("Duplicate network name 'default' in environment 'new'");
        }


        public void Dispose()
        {
            _connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }


        private readonly Guid _projectId = Guid.NewGuid();

        private readonly NetworkProvider[] _providers = {
            new()
            {
                Name = "default",
                TypeString = "nat_overlay",
                Subnets = new NetworkProviderSubnet[]
                {
                    new()
                    {
                        Name = "default",
                        Gateway = "192.168.100.1",
                        IpPools = new NetworkProviderIpPool[]
                        {
                            new()
                            {
                                Name = "default",
                                FirstIp = "192.168.100.10",
                                LastIp = "192.168.100.20"
                            }
                        }
                    }
                }
            },
            new()
            {
                Name = "flat",
                TypeString = "flat"
            }
        };

        private readonly ProjectNetworksConfig _projectConfig = new()
        {
            Project = "project1",
            Networks = new[]
            {
                new NetworkConfig
                {
                    Name = "unused_network",
                    Environment = "default",
                    Address = "192.168.10.0/24",
                    Provider = new ProviderConfig
                    {
                        Name = "default",
                        IpPool = "default",
                        Subnet = "default"
                    }, 
                    Subnets = new []
                    {
                        new NetworkSubnetConfig
                        {
                            Name = "default",
                            Address = "192.168.10.0/24"
                        }
                    }

                },
                new NetworkConfig
                {
                    Name = "used_network",
                    Environment = "default",
                    Address = "192.168.11.0/24",
                    Provider = new ProviderConfig
                    {
                        Name = "default",
                        IpPool = "default",
                        Subnet = "default"
                    }
                },
                new NetworkConfig
                {
                    Name = "pool_network",
                    Environment = "default",
                    Address = "192.168.15.0/24",
                    Subnets = new []
                        {
                            new NetworkSubnetConfig
                            {
                                Name = "default",
                                Address = "192.168.15.0/24",
                                IpPools = new []
                                {
                                    new IpPoolConfig
                                    {
                                        Name = "pool1",
                                        FirstIp = "192.168.15.10",
                                        LastIp = "192.168.15.15"
                                    },
                                    new IpPoolConfig
                                    {
                                        Name = "pool2",
                                        FirstIp = "192.168.15.100",
                                        LastIp = "192.168.15.120"
                                    }
                                }
                            }
                        },
                    Provider = new ProviderConfig
                    {
                        Name = "default",
                        IpPool = "default",
                        Subnet = "default"
                    }
                }
            }

        };

        private async Task<string[]> RunChangeValidator(ProjectNetworksConfig projectConfig)
        {
            var contextOptions = new DbContextOptionsBuilder<SqliteStateStoreContext>()
                .UseSqlite(_connection)
                .Options;

            await using var context = new SqliteStateStoreContext(contextOptions);
            var stateStore = new StateStore(context);
            await context.Database.EnsureCreatedAsync();
            await SeedData(stateStore);


            var configValidator = new NetworkConfigValidator(stateStore, NullLogger.Instance);
            var res = await configValidator.ValidateChanges(_projectId, projectConfig, _providers).ToListAsync();
            foreach (var message in res)
            {
                _output.WriteLine(message);
            }

            return res.ToArray();

        }

        private string[] RunConfigValidator(ProjectNetworksConfig projectConfig)
        {

            var configValidator = new NetworkConfigValidator(null!, NullLogger.Instance);
            var normalized = configValidator.NormalizeConfig(projectConfig);
            var res = configValidator.ValidateConfig(normalized, _providers).ToArray();
            foreach (var message in res)
            {
                _output.WriteLine(message);
            }

            return res;

        }

        private async Task SeedData(IStateStore stateStore)
        {
            var networkRepo = stateStore.For<VirtualNetwork>();

            var project = new Project
            {
                Id = _projectId,
                Name = "project1",
                Tenant = new Tenant
                {
                    Id = EryphConstants.DefaultTenantId
                }
            };

            var firstCatletMetadata = new CatletMetadata();
            await stateStore.For<CatletMetadata>().AddAsync(firstCatletMetadata);
            var secondCatletMetadata = new CatletMetadata();
            await stateStore.For<CatletMetadata>().AddAsync(secondCatletMetadata);

            await networkRepo.AddAsync(
                new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    Name = "unused_network",
                    ProjectId = _projectId,
                    Environment = "default",
                    IpNetwork = "192.168.10.0/24",
                    NetworkProvider = "provider",
                    Project = project
                });

            await networkRepo.AddAsync(
                new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    Name = "used_network",
                    ProjectId = _projectId,
                    Environment = "default",
                    IpNetwork = "192.168.11.0/24",
                    NetworkProvider = "provider",
                    Project = project,
                    NetworkPorts = new VirtualNetworkPort[]
                    {
                        new CatletNetworkPort()
                        {
                            Name = "test-catlet-port",
                            MacAddress = "00:00:00:00:00:10",
                            CatletMetadataId = firstCatletMetadata.Id,
                        },
                        new ProviderRouterPort()
                        {
                            Name = "provider",
                            MacAddress = "00:00:00:00:00:01",
                            ProviderName = "default",
                            PoolName = "default",
                            SubnetName = "default"
                        }
                    }.ToList()
                });

            var catletNetworkPortId = Guid.NewGuid();

            await networkRepo.AddAsync(
                new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    Name = "pool_network",
                    ProjectId = _projectId,
                    Environment = "default",
                    IpNetwork = "192.168.15.0/24",
                    NetworkProvider = "provider",
                    Project = project,
                    Subnets = new[]
                    {
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.NewGuid(),
                            Name = "default",
                            IpNetwork = "192.168.15.0/24",
                            IpPools = new[]
                            {
                                new IpPool()
                                {
                                    Name = "pool1",
                                    FirstIp = "192.168.15.10",
                                    NextIp = "192.168.15.10",
                                    LastIp = "192.168.15.15",
                                    IpNetwork = "192.168.15.0/24",
                                },
                                new IpPool()
                                {
                                    Name = "pool2",
                                    FirstIp = "192.168.15.100",
                                    NextIp = "192.168.15.105",
                                    LastIp = "192.168.15.120",
                                    IpNetwork = "192.168.15.0/24",
                                    IpAssignments = new []
                                    {
                                        new IpPoolAssignment
                                        {
                                            NetworkPortId = catletNetworkPortId,
                                            IpAddress = "192.168.15.110",
                                            Number = 11,
                                        },
                                    }.ToList()
                                }
                            }.ToList()
                        }
                    }.ToList(),
                    NetworkPorts = new VirtualNetworkPort[]
                    {
                        new CatletNetworkPort()
                        {
                            Id = catletNetworkPortId,
                            MacAddress = "00:00:00:00:10:10",
                            Name = "test-catlet-port",
                            CatletMetadataId = secondCatletMetadata.Id,
                        },
                        new ProviderRouterPort()
                        {
                            Name = "provider",
                            MacAddress = "00:00:00:00:10:01",
                            ProviderName = "default",
                            PoolName = "default",
                            SubnetName = "default"
                        }
                    }.ToList()
                });

            await stateStore.SaveChangesAsync();


        }
    }
}
