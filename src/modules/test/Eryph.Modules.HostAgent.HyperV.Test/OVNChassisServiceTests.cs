using System.Net;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.ModuleCore.Components;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class OVNChassisServiceTests
{
    [Fact]
    public void BuildChassisPlan_NoProviders_PlanHasChassisIdAndTunnelButNoBridgeMappings()
    {
        var config = new NetworkProvidersConfiguration { NetworkProviders = null };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.ChassisId.Should().Be(EryphConstants.Networking.LocalChassisName);
        plan.TunnelEndpoints.Should().HaveCount(1);
        plan.TunnelEndpoints[0].EncapsulationType.Should().Be("geneve");
        plan.TunnelEndpoints[0].IpAddress.Should().Be(IPAddress.Loopback);
        plan.BridgeMappings.Should().BeEmpty();
    }

    [Fact]
    public void BuildChassisPlan_EmptyProviders_NoBridgeMappings()
    {
        var config = new NetworkProvidersConfiguration { NetworkProviders = [] };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Should().BeEmpty();
    }

    [Fact]
    public void BuildChassisPlan_OnlyFlatProviders_NoBridgeMappings()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "flat1", Type = NetworkProviderType.Flat, BridgeName = "br-flat1" },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Should().BeEmpty();
    }

    [Fact]
    public void BuildChassisPlan_OverlayProvider_BridgeMappingAdded()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "default", Type = NetworkProviderType.Overlay, BridgeName = "br-int" },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Find("default").IfNoneUnsafe((string?)null).Should().Be("br-int");
    }

    [Fact]
    public void BuildChassisPlan_NatOverlayProvider_BridgeMappingAdded()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "default", Type = NetworkProviderType.NatOverlay, BridgeName = "br-nat" },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Find("default").IfNoneUnsafe((string?)null).Should().Be("br-nat");
    }

    [Fact]
    public void BuildChassisPlan_OverlayWithoutBridgeName_Skipped()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "default", Type = NetworkProviderType.Overlay, BridgeName = null },
                new NetworkProvider { Name = "empty", Type = NetworkProviderType.Overlay, BridgeName = "" },
                new NetworkProvider { Name = "whitespace", Type = NetworkProviderType.Overlay, BridgeName = "  " },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Should().BeEmpty();
    }

    [Fact]
    public void BuildChassisPlan_MixedProviders_OnlyOverlayAndNatOverlayIncluded()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "nat", Type = NetworkProviderType.NatOverlay, BridgeName = "br-nat" },
                new NetworkProvider { Name = "flat", Type = NetworkProviderType.Flat, BridgeName = "br-flat" },
                new NetworkProvider { Name = "tun", Type = NetworkProviderType.Overlay, BridgeName = "br-tun" },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Should().HaveCount(2);
        plan.BridgeMappings.Find("nat").IfNoneUnsafe((string?)null).Should().Be("br-nat");
        plan.BridgeMappings.Find("tun").IfNoneUnsafe((string?)null).Should().Be("br-tun");
        plan.BridgeMappings.Find("flat").IsNone.Should().BeTrue();
    }

    [Fact]
    public void BuildChassisPlan_ConfiguredEncapIp_UsedAsTunnelEndpoint()
    {
        var config = new NetworkProvidersConfiguration { NetworkProviders = null };
        var encapIp = IPAddress.Parse("10.0.0.21");

        var plan = OVNChassisService.BuildChassisPlan(config, encapIp, southbound: null);

        plan.TunnelEndpoints.Should().HaveCount(1);
        plan.TunnelEndpoints[0].EncapsulationType.Should().Be("geneve");
        plan.TunnelEndpoints[0].IpAddress.Should().Be(encapIp);
    }

    [Fact]
    public void BuildChassisPlan_NoSouthbound_LeavesSouthboundNonSsl()
    {
        var config = new NetworkProvidersConfiguration { NetworkProviders = null };

        var plan = OVNChassisService.BuildChassisPlan(config, IPAddress.Loopback, southbound: null);

        // Co-located: the southbound connection is not switched to SSL and no OVS SSL material is set.
        plan.SouthboundDatabase.Ssl.Should().BeFalse();
        plan.PlannedSwitchSsl.Should().BeNull();
    }

    [Fact]
    public void BuildChassisPlan_RemoteSouthbound_SetsOvnRemoteSslAndSwitchSslAndEncapIp()
    {
        var config = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider { Name = "default", Type = NetworkProviderType.Overlay, BridgeName = "br-int" },
            ],
        };
        var encapIp = IPAddress.Parse("10.0.0.21");
        var pem = new ComponentCertificatePem("KEY-PEM", "CERT-PEM", "CA-PEM");
        // The address is a resolved IP literal — OVS on Windows cannot dial a host name for 'ssl:'.
        var southbound = new OVNChassisService.ChassisSouthbound("10.0.0.9", 6642, pem);

        var plan = OVNChassisService.BuildChassisPlan(config, encapIp, southbound);

        // ovn-remote points at the advertised SSL endpoint (resolved to an IP literal).
        plan.SouthboundDatabase.Address.Should().Be("10.0.0.9");
        plan.SouthboundDatabase.Port.Should().Be(6642);
        plan.SouthboundDatabase.Ssl.Should().BeTrue();

        // The OVS SSL table carries the agent's enrolled certificate material (ovn-controller reads it
        // from there for the southbound connection).
        plan.PlannedSwitchSsl.Should().NotBeNull();
        plan.PlannedSwitchSsl!.PrivateKey.Should().Be("KEY-PEM");
        plan.PlannedSwitchSsl.Certificate.Should().Be("CERT-PEM");
        plan.PlannedSwitchSsl.CaCertificate.Should().Be("CA-PEM");

        // The overlay tunnel endpoint uses the configured host IP, not loopback.
        plan.TunnelEndpoints[0].IpAddress.Should().Be(encapIp);

        // Bridge mappings are still applied on top of the SSL configuration.
        plan.BridgeMappings.Find("default").IfNoneUnsafe((string?)null).Should().Be("br-int");
    }

    [Theory]
    [InlineData("10.0.0.9", "10.0.0.9")]   // an IPv4 literal passes through unchanged
    [InlineData("[fe80::1]", "[fe80::1]")] // a bracketed IPv6 literal stays bracketed for 'ssl:host:port'
    public async Task ResolveToIp_IpLiteral_PreservesBracketingForRemote(string host, string expected)
    {
        // ovn-remote is an 'ssl:host:port' string, so an IPv6 address must remain bracketed to stay
        // unambiguous with the port separator; an IPv4 address must not be bracketed.
        (await OVNChassisService.ResolveToIp(host, CancellationToken.None)).Should().Be(expected);
    }
}
