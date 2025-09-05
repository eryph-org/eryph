using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using Eryph.VmManagement.Sys;
using Eryph.VmManagement.Wmi;
using FluentAssertions;
using LanguageExt;
using FluentAssertions.LanguageExt;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.HyperV.Test.Networking;

public class VirtualNetworkQueryTests
{
    private readonly Mock<WmiIO> _wmiIOMock = new();

    private const string AdapterId =
        @"Microsoft:2FE70974-C81A-4F3A-BF4E-7BE405B88C97\596574F5-A810-43EF-B349-D20783874CE5";

    [Fact]
    public void GetNetworkByAdapter_ValidNetworkData_ReturnsData()
    {
        ArrangeData();

        var result = VirtualNetworkQuery<TestRuntime>.getNetworkByAdapter(
                new VMNetworkAdapter()
                {
                    Id = AdapterId,
                    Name = "eth0",
                    MacAddress = "001122334455",
                }, "test-port")
            .Run(TestRuntime.New(_wmiIOMock.Object));

        result.ThrowIfFail();

        var networkData = result.Should().BeSuccess().Subject;
        networkData.AdapterName.Should().Be("eth0");
        networkData.PortName.Should().Be("test-port");
        networkData.MacAddress.Should().Be("00:11:22:33:44:55");
        networkData.DefaultGateways.Should().Equal("10.0.0.1");
        networkData.DnsServers.Should().Equal("10.0.0.2");
        networkData.DhcpEnabled.Should().BeTrue();
        networkData.IPAddresses.Should().Equal("10.0.0.100", "fe80::d0ab:1ff:fed0:501");
        networkData.Subnets.Should().Equal("10.0.0.0/20", "fe80::/64");
    }

    [Fact]
    public void GetNetworkByAdapter_MacAddressNotSet_ReturnsDataWithoutMacAddress()
    {
        ArrangeData();

        var result = VirtualNetworkQuery<TestRuntime>.getNetworkByAdapter(
                new VMNetworkAdapter()
                {
                    Id = AdapterId,
                    Name = "eth0",
                    MacAddress = "000000000000",
                }, "test-port")
            .Run(TestRuntime.New(_wmiIOMock.Object));

        result.ThrowIfFail();

        var networkData = result.Should().BeSuccess().Subject;
        networkData.AdapterName.Should().Be("eth0");
        networkData.PortName.Should().Be("test-port");
        networkData.MacAddress.Should().BeNull();
        networkData.DefaultGateways.Should().Equal("10.0.0.1");
        networkData.DnsServers.Should().Equal("10.0.0.2");
        networkData.DhcpEnabled.Should().BeTrue();
        networkData.IPAddresses.Should().Equal("10.0.0.100", "fe80::d0ab:1ff:fed0:501");
        networkData.Subnets.Should().Equal("10.0.0.0/20", "fe80::/64");
    }

    private void ArrangeData()
    {
        _wmiIOMock.Setup(m => m.ExecuteQuery(
                @"root\virtualization\v2",
                Seq("DefaultGateways", "DHCPEnabled", "DNSServers", "IPAddresses", "Subnets"),
                "Msvm_GuestNetworkAdapterConfiguration",
                @"InstanceID = 'Microsoft:GuestNetwork\\2FE70974-C81A-4F3A-BF4E-7BE405B88C97\\596574F5-A810-43EF-B349-D20783874CE5'"))
            .Returns(FinSucc(Seq1(new WmiObject(HashMap(
                ("DefaultGateways", Optional<object>(new[] { "10.0.0.1" })),
                ("DNSServers", Optional<object>(new[] { "10.0.0.2" })),
                ("DHCPEnabled", Optional<object>(true)),
                ("IPAddresses", Optional<object>(new[] { "10.0.0.100", "fe80::d0ab:1ff:fed0:501" })),
                ("Subnets", Optional<object>(new[] { "255.255.240.0", "/64" }))
                )))));
    }

    private readonly struct TestRuntime(WmiIO wmiIO) : HasWmi<TestRuntime>
    {
        private readonly WmiIO _wmiIO = wmiIO;

        public static TestRuntime New(WmiIO wmiIO) => new(wmiIO);

        public Eff<TestRuntime, WmiIO> WmiEff => Eff<TestRuntime, WmiIO>(rt => rt._wmiIO);
    }
}
