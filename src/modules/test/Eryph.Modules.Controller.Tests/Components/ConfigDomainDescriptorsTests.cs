using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies which domains may be operator-authored and that YAML payloads are validated and
/// canonicalized: the guard that keeps a wrong-domain or malformed write out of the distributed
/// configuration.
/// </summary>
public class ConfigDomainDescriptorsTests
{
    [Fact]
    public void StorageConfig_is_authorable() =>
        ConfigDomainDescriptors.IsAuthorable(ConfigDomain.StorageConfig).Should().BeTrue();

    [Theory]
    [InlineData(ConfigDomain.NetworkProviders)]
    [InlineData(ConfigDomain.Endpoints)]
    [InlineData(ConfigDomain.OvnCluster)]
    public void System_derived_domains_are_not_authorable(ConfigDomain domain) =>
        ConfigDomainDescriptors.IsAuthorable(domain).Should().BeFalse();

    [Fact]
    public void TryCanonicalize_accepts_valid_yaml_and_normalizes_whitespace_and_order()
    {
        // Flow style, reversed key order.
        var a = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.StorageConfig, "environments: [{name: e1}]\ndatastores: [{name: ds1}]", out var canonicalA);
        // Block style, declaration order, trailing newline.
        var b = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.StorageConfig, "datastores:\n- name: ds1\nenvironments:\n- name: e1\n", out var canonicalB);

        a.Should().BeTrue();
        b.Should().BeTrue();
        // Semantically-identical payloads canonicalize identically, so they will not create noisy versions.
        canonicalA.Should().Be(canonicalB);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("42")] // a scalar, not a mapping
    [InlineData("datastores:\n- name: ds1\nunknown_key: x")] // unknown member — rejected (strict)
    [InlineData("""{"Datastores":["ds1"]}""")] // wrong-cased key ('datastores' is the underscored name)
    public void TryCanonicalize_rejects_an_invalid_payload(string payload) =>
        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.StorageConfig, payload, out _).Should().BeFalse();

    [Fact]
    public void TryCanonicalize_rejects_a_non_authorable_domain() =>
        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.NetworkProviders, "datastores: [ds1]", out _)
            .Should().BeFalse();
}
