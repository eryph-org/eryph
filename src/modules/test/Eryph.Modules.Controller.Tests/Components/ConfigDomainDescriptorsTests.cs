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
    [Theory]
    [InlineData(ConfigDomain.StorageConfig)]
    [InlineData(ConfigDomain.NetworkProviders)]
    public void Operator_authorable_domains(ConfigDomain domain) =>
        ConfigDomainDescriptors.IsAuthorable(domain).Should().BeTrue();

    [Theory]
    [InlineData(ConfigDomain.Endpoints)]
    [InlineData(ConfigDomain.OvnCluster)]
    public void System_derived_domains_are_not_authorable(ConfigDomain domain) =>
        ConfigDomainDescriptors.IsAuthorable(domain).Should().BeFalse();

    [Fact]
    public void NetworkProviders_canonicalization_validates_and_strips_the_ip_pool_cursor()
    {
        // A valid overlay provider with an IP pool carrying a runtime next-IP cursor.
        const string payload =
            "network_providers:\n"
            + "- name: default\n"
            + "  type: nat_overlay\n"
            + "  bridge_name: br-nat\n"
            + "  subnets:\n"
            + "  - name: default\n"
            + "    network: 10.249.248.0/22\n"
            + "    gateway: 10.249.248.1\n"
            + "    ip_pools:\n"
            + "    - name: default\n"
            + "      first_ip: 10.249.248.10\n"
            + "      next_ip: 10.249.248.42\n"
            + "      last_ip: 10.249.251.254\n";

        var ok = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.NetworkProviders, payload, out var canonical);

        ok.Should().BeTrue();
        // The cursor is runtime state, not authored config, so it is stripped from the canonical form.
        canonical.Should().NotContain("next_ip");
        canonical.Should().Contain("first_ip");
    }

    [Fact]
    public void NetworkProviders_canonicalization_rejects_a_semantically_invalid_payload()
    {
        // Overlapping NAT subnets across providers — rejected by NetworkProvidersConfigValidations,
        // which the serializer's shape check alone would not catch.
        const string payload =
            "network_providers:\n"
            + "- name: default\n"
            + "  type: nat_overlay\n"
            + "  bridge_name: br-a\n"
            + "  subnets:\n"
            + "  - name: default\n"
            + "    network: 10.249.248.0/22\n"
            + "- name: second\n"
            + "  type: nat_overlay\n"
            + "  bridge_name: br-b\n"
            + "  subnets:\n"
            + "  - name: default\n"
            + "    network: 10.249.248.0/22\n";

        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.NetworkProviders, payload, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void TryCanonicalize_returns_the_specific_validation_error_for_the_operator()
    {
        // Overlapping NAT subnets — the operator should get the validation detail, not a generic message.
        const string payload =
            "network_providers:\n"
            + "- name: default\n"
            + "  type: nat_overlay\n"
            + "  bridge_name: br-a\n"
            + "  subnets:\n"
            + "  - name: default\n"
            + "    network: 10.249.248.0/22\n"
            + "- name: second\n"
            + "  type: nat_overlay\n"
            + "  bridge_name: br-b\n"
            + "  subnets:\n"
            + "  - name: default\n"
            + "    network: 10.249.248.0/22\n";

        var ok = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.NetworkProviders, payload, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("network provider configuration is invalid");
    }

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
