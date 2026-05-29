using System.Net;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent;
using FluentAssertions;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

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
                new NetworkProvider { Name = "nat",  Type = NetworkProviderType.NatOverlay, BridgeName = "br-nat" },
                new NetworkProvider { Name = "flat", Type = NetworkProviderType.Flat,       BridgeName = "br-flat" },
                new NetworkProvider { Name = "tun",  Type = NetworkProviderType.Overlay,    BridgeName = "br-tun" },
            ],
        };

        var plan = OVNChassisService.BuildChassisPlan(config);

        plan.BridgeMappings.Should().HaveCount(2);
        plan.BridgeMappings.Find("nat").IfNoneUnsafe((string?)null).Should().Be("br-nat");
        plan.BridgeMappings.Find("tun").IfNoneUnsafe((string?)null).Should().Be("br-tun");
        plan.BridgeMappings.Find("flat").IsNone.Should().BeTrue();
    }
}
