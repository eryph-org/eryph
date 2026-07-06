using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies which domains may be operator-authored and that payloads are validated and canonicalized:
/// the guard that keeps a wrong-domain or malformed write out of the distributed configuration.
/// </summary>
public class ConfigDomainDescriptorsTests
{
    [Fact]
    public void PlacementConfig_is_authorable() =>
        ConfigDomainDescriptors.IsAuthorable(ConfigDomain.PlacementConfig).Should().BeTrue();

    [Theory]
    [InlineData(ConfigDomain.NetworkProviders)]
    [InlineData(ConfigDomain.Endpoints)]
    [InlineData(ConfigDomain.OvnCluster)]
    public void System_derived_domains_are_not_authorable(ConfigDomain domain) =>
        ConfigDomainDescriptors.IsAuthorable(domain).Should().BeFalse();

    [Fact]
    public void TryCanonicalize_normalizes_whitespace_and_property_order()
    {
        var a = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.PlacementConfig, """{  "Environments" : ["e1"] , "Datastores":  ["ds1"] }""", out var canonicalA);
        var b = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.PlacementConfig, """{"Datastores":["ds1"],"Environments":["e1"]}""", out var canonicalB);

        a.Should().BeTrue();
        b.Should().BeTrue();
        // Semantically-identical payloads canonicalize identically, so they will not create noisy versions.
        canonicalA.Should().Be(canonicalB);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("""{"datastores":["ds1"]}""")] // wrong-cased member — would deserialize to an empty vocabulary
    public void TryCanonicalize_rejects_an_invalid_payload(string payload) =>
        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.PlacementConfig, payload, out _).Should().BeFalse();

    [Fact]
    public void TryCanonicalize_rejects_a_non_authorable_domain() =>
        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.Endpoints, "{}", out _).Should().BeFalse();
}
