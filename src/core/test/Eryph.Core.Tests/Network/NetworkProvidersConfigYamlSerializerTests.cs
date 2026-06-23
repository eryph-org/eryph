using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Network;

namespace Eryph.Core.Tests.Network;

public class NetworkProvidersConfigYamlSerializerTests
{
    [Fact]
    public void Deserialize_DefaultConfig_ReturnsConfig()
    {
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(
            NetworkProvidersConfiguration.DefaultConfig);

        config.Should().NotBeNull();
        config.NetworkProviders.Should().HaveCount(1);
    }

    [Fact]
    public void Deserialize_FlatProviderWithSubnet_ReturnsConfig()
    {
        var yaml = """
                   network_providers:
                   - name: flat-provider
                     type: flat
                     switch_name: test-switch
                     subnets:
                     - name: default
                       network: 192.168.2.0/24
                       gateway: 192.168.2.1
                       dns_servers:
                       - 9.9.9.9
                       - 8.8.8.8
                       dns_domain: example.com
                       mtu: 1400
                       ip_pools:
                       - name: default
                         first_ip: 192.168.2.100
                         next_ip: 192.168.2.100
                         last_ip: 192.168.2.150
                   """;
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(yaml);

        config.NetworkProviders.Should().SatisfyRespectively(
            provider =>
            {
                provider.Name.Should().Be("flat-provider");
                provider.Type.Should().Be(NetworkProviderType.Flat);
                provider.SwitchName.Should().Be("test-switch");
                provider.Subnets.Should().SatisfyRespectively(
                    subnet =>
                    {
                        subnet.Name.Should().Be("default");
                        subnet.Network.Should().Be("192.168.2.0/24");
                        subnet.Gateway.Should().Be("192.168.2.1");
                        subnet.DnsServers.Should().Equal("9.9.9.9", "8.8.8.8");
                        subnet.DnsDomain.Should().Be("example.com");
                        subnet.Mtu.Should().Be(1400);
                    });
            });

        // Round-trip: re-serializing and deserializing must preserve the new settings.
        var roundTripped = NetworkProvidersConfigYamlSerializer.Deserialize(
            NetworkProvidersConfigYamlSerializer.Serialize(config));
        roundTripped.NetworkProviders[0].Subnets[0].DnsServers.Should().Equal("9.9.9.9", "8.8.8.8");
        roundTripped.NetworkProviders[0].Subnets[0].DnsDomain.Should().Be("example.com");
        roundTripped.NetworkProviders[0].Subnets[0].Mtu.Should().Be(1400);
    }

    [Fact]
    public void Deserialize_ComplexConfig_ReturnsConfig()
    {
        var yaml = """
                   network_providers:
                   - name: test-provider
                     type: overlay
                     bridge_name: br-pif
                     vlan: 42
                     bridge_options:
                       bridge_vlan: 43
                       vlan_mode: native_tagged
                       default_ip_mode: dhcp
                       bond_mode: balance_slb
                     adapters:
                     - test-adapter-1
                     - test-adapter-2
                     subnets:
                     - name: default
                       network: 10.249.248.0/22
                       gateway: 10.249.248.1
                       ip_pools:
                       - name: default
                         first_ip: 10.249.248.10
                         next_ip: 10.249.248.42
                         last_ip: 10.249.251.241
                   """;
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(yaml);

        config.NetworkProviders.Should().SatisfyRespectively(
            provider =>
            {
                provider.Name.Should().Be("test-provider");
                provider.Type.Should().Be(NetworkProviderType.Overlay);
                provider.BridgeName.Should().Be("br-pif");
                provider.Vlan.Should().Be(42);
                provider.Adapters.Should().Equal("test-adapter-1", "test-adapter-2");
                provider.BridgeOptions.Should().NotBeNull();
                provider.BridgeOptions!.BridgeVlan.Should().Be(43);
                provider.BridgeOptions!.VlanMode.Should().Be(BridgeVlanMode.NativeTagged);
                provider.BridgeOptions!.DefaultIpMode.Should().Be(BridgeHostIpMode.Dhcp);
                provider.BridgeOptions!.BondMode.Should().Be(BondMode.BalanceSlb);
                provider.Subnets.Should().SatisfyRespectively(
                    subnet =>
                    {
                        subnet.Name.Should().Be("default");
                        subnet.Network.Should().Be("10.249.248.0/22");
                        subnet.Gateway.Should().Be("10.249.248.1");
                        subnet.IpPools.Should().SatisfyRespectively(
                            pool =>
                            {
                                pool.Name.Should().Be("default");
                                pool.FirstIp.Should().Be("10.249.248.10");
                                pool.NextIp.Should().Be("10.249.248.42");
                                pool.LastIp.Should().Be("10.249.251.241");
                            });
                    });
            });
    }

    [Fact]
    public void Serialize_DeserializedDefaultConfig_ReturnsSameYaml()
    {
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(
            NetworkProvidersConfiguration.DefaultConfig);

        var yaml = NetworkProvidersConfigYamlSerializer.Serialize(config);

        yaml.TrimEnd().Should().Be(NetworkProvidersConfiguration.DefaultConfig);
    }
}
