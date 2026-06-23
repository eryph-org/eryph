using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using FluentAssertions;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.HyperV.Test.Converging;

public class CloudInitNetworkDataTests
{
    private static Dictionary<string, object> CreateConfig(
        Seq<CatletNetworkConfig> networks,
        Seq<MachineNetworkSettings> settings) =>
        (Dictionary<string, object>)CloudInitNetworkData.CreateAdapterConfig(
            "eth0", "00:11:22:33:44:55", networks, settings);

    [Fact]
    public void CreateAdapterConfig_NoStaticSettings_UsesDhcp()
    {
        var networks = Seq1(new CatletNetworkConfig { AdapterName = "eth0", Name = "default" });
        var settings = Seq1(new MachineNetworkSettings
        {
            AdapterName = "eth0",
            NetworkName = "default",
            AddressesV4 = ["10.0.0.5"],
        });

        var config = CreateConfig(networks, settings);

        config["type"].Should().Be("physical");
        config["name"].Should().Be("eth0");
        config["mac_address"].Should().Be("00:11:22:33:44:55");
        config.Should().NotContainKey("mtu");

        var subnets = config["subnets"].Should().BeAssignableTo<object[]>().Subject;
        subnets.Should().ContainSingle();
        var subnet = subnets[0].Should().BeOfType<Dictionary<string, object>>().Subject;
        subnet["type"].Should().Be("dhcp");
    }

    [Fact]
    public void CreateAdapterConfig_StaticSettings_GeneratesStaticSubnet()
    {
        var networks = Seq1(new CatletNetworkConfig { AdapterName = "eth0", Name = "flat-static-network" });
        var settings = Seq1(new MachineNetworkSettings
        {
            AdapterName = "eth0",
            NetworkName = "flat-static-network",
            AddressesV4 = ["192.168.5.12"],
            PrefixLengthV4 = 24,
            GatewayV4 = "192.168.5.1",
            DnsServersV4 = ["9.9.9.9", "8.8.8.8"],
            DnsDomain = "example.com",
            Mtu = 1400,
        });

        var config = CreateConfig(networks, settings);

        config["mtu"].Should().Be(1400);

        var subnets = config["subnets"].Should().BeAssignableTo<object[]>().Subject;
        var subnet = subnets.Should().ContainSingle().Subject
            .Should().BeOfType<Dictionary<string, object>>().Subject;
        subnet["type"].Should().Be("static");
        subnet["address"].Should().Be("192.168.5.12/24");
        subnet["gateway"].Should().Be("192.168.5.1");
        subnet["dns_nameservers"].Should().BeEquivalentTo(new[] { "9.9.9.9", "8.8.8.8" });
        subnet["dns_search"].Should().BeEquivalentTo(new[] { "example.com" });
    }

    [Fact]
    public void CreateAdapterConfig_StaticWithoutDnsOrMtu_OmitsOptionalKeys()
    {
        var networks = Seq1(new CatletNetworkConfig { AdapterName = "eth0", Name = "flat-static-network" });
        var settings = Seq1(new MachineNetworkSettings
        {
            AdapterName = "eth0",
            NetworkName = "flat-static-network",
            AddressesV4 = ["192.168.5.12"],
            PrefixLengthV4 = 24,
            GatewayV4 = "192.168.5.1",
        });

        var config = CreateConfig(networks, settings);

        config.Should().NotContainKey("mtu");

        var subnets = config["subnets"].Should().BeAssignableTo<object[]>().Subject;
        var subnet = subnets[0].Should().BeOfType<Dictionary<string, object>>().Subject;
        subnet["type"].Should().Be("static");
        subnet.Should().NotContainKey("dns_nameservers");
        subnet.Should().NotContainKey("dns_search");
    }

    [Fact]
    public void CreateAdapterConfig_GatewayMissing_FallsBackToDhcp()
    {
        // An address without a gateway/prefix (e.g. overlay networks) must not be configured
        // statically - those rely on the eryph-managed DHCP server.
        var networks = Seq1(new CatletNetworkConfig { AdapterName = "eth0", Name = "default" });
        var settings = Seq1(new MachineNetworkSettings
        {
            AdapterName = "eth0",
            NetworkName = "default",
            AddressesV4 = ["10.0.0.5"],
            PrefixLengthV4 = 24,
        });

        var config = CreateConfig(networks, settings);

        var subnets = config["subnets"].Should().BeAssignableTo<object[]>().Subject;
        var subnet = subnets[0].Should().BeOfType<Dictionary<string, object>>().Subject;
        subnet["type"].Should().Be("dhcp");
    }
}
