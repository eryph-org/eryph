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
    [InlineData(ConfigDomain.Environments)]
    public void Operator_authorable_domains(ConfigDomain domain) =>
        ConfigDomainDescriptors.IsAuthorable(domain).Should().BeTrue();

    [Fact]
    public void Environments_an_omitted_site_is_filled_with_the_default_site()
    {
        ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.Environments,
                """
                environments:
                - name: staging
                """,
                out var canonical)
            .Should().BeTrue();

        canonical.Should().Contain("site: default");
    }

    [Fact]
    public void Environments_names_are_lower_cased()
    {
        ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.Environments,
                """
                sites:
                - name: Berlin
                environments:
                - name: Staging
                  site: Berlin
                """,
                out var canonical)
            .Should().BeTrue();

        canonical.Should().Contain("name: staging").And.Contain("site: berlin");
    }

    [Fact]
    public void Environments_a_site_which_is_not_declared_is_rejected()
    {
        ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.Environments,
                """
                environments:
                - name: staging
                  site: berlin
                """,
                out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("'berlin', which is not declared");
    }

    [Fact]
    public void Environments_the_reserved_default_environment_is_rejected()
    {
        ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.Environments,
                """
                environments:
                - name: default
                  site: berlin
                """,
                out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("reserved");
    }

    [Fact]
    public void Environments_are_global_and_cannot_be_scoped()
    {
        ConfigDomainDescriptors.SupportsScopedAuthoring(ConfigDomain.Environments).Should().BeFalse();
    }

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
        // Endpoints is system-derived (built from controller state), unlike NetworkProviders which is
        // authorable — using it here actually exercises the "not authorable" branch.
        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.Endpoints, "datastores: [ds1]", out _)
            .Should().BeFalse();

    [Fact]
    public void StorageConfig_canonicalization_lower_cases_the_datastore_name()
    {
        const string payload = "datastores: [{name: Fast, path: \"D:\\\\fast\"}]";

        var ok = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.StorageConfig, payload, out var canonical);

        ok.Should().BeTrue();
        canonical.Should().Contain("name: fast");
    }

    [Fact]
    public void StorageConfig_canonicalization_rejects_an_invalid_datastore_name()
    {
        const string payload = "datastores: [{name: \"bad name!\", path: \"D:\\\\x\"}]";

        var ok = ConfigDomainDescriptors.TryCanonicalize(
            ConfigDomain.StorageConfig, payload, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("invalid");
    }

    [Fact]
    public void StorageConfig_canonicalization_rejects_a_non_fully_qualified_path()
    {
        const string payload = "datastores: [{name: fast, path: \"relative\\\\dir\"}]";

        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.StorageConfig, payload, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void StorageConfig_canonicalization_rejects_a_duplicate_datastore_name()
    {
        const string payload =
            "datastores: [{name: fast, path: \"D:\\\\a\"}, {name: fast, path: \"D:\\\\b\"}]";

        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.StorageConfig, payload, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void StorageConfig_canonicalization_rejects_a_duplicate_datastore_path()
    {
        const string payload =
            "datastores: [{name: a, path: \"D:\\\\same\"}, {name: b, path: \"D:\\\\same\"}]";

        ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.StorageConfig, payload, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void StorageConfig_canonicalization_treats_a_null_document_as_empty_without_throwing()
    {
        // StorageConfigYamlSerializer.Deserialize coalesces a null document ("~") to an empty
        // StorageConfig, so canonicalization succeeds instead of crashing the handler with an NRE.
        var ok = ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.StorageConfig, "~", out var canonical, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        canonical.Should().NotBeNull();
    }

    [Fact]
    public void NetworkProviders_canonicalization_rejects_a_null_document_without_throwing()
    {
        var ok = ConfigDomainDescriptors.TryCanonicalize(ConfigDomain.NetworkProviders, "~", out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }
}
