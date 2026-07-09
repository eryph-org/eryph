using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-scope selector grammar and, most importantly, the resolution order a component
/// walks: <c>host &gt; tag &gt; env &gt; default</c>, with tags ordered deterministically by key.
/// </summary>
public class ConfigScopeTests
{
    private static ComponentRegistration Reg(
        Guid componentId, string? environment = null, params (string Key, string Value)[] tags) =>
        new()
        {
            ComponentId = componentId,
            ComponentType = ComponentType.VMHostAgent,
            MachineName = "host",
            InboundQueue = "queue",
            Environment = environment,
            Tags = tags.ToDictionary(t => t.Key, t => t.Value),
        };

    [Fact]
    public void Selectors_are_formatted_per_the_grammar()
    {
        ConfigScope.Default.Should().Be("");
        ConfigScope.ForEnvironment("prod").Should().Be("env:prod");
        ConfigScope.ForTag("rack", "r1").Should().Be("tag:rack=r1");
        ConfigScope.ForHost(Guid.Empty).Should().Be("host:00000000-0000-0000-0000-000000000000");
    }

    [Fact]
    public void ResolutionOrder_is_host_then_tags_then_env_then_default()
    {
        var id = Guid.NewGuid();
        var registration = Reg(id, "prod", ("rack", "r1"), ("zone", "z1"));

        ConfigScope.ResolutionOrder(registration).Should().Equal(
            $"host:{id}",
            "tag:rack=r1",   // tags ordered by key: rack < zone
            "tag:zone=z1",
            "env:prod",
            "");
    }

    [Fact]
    public void ResolutionOrder_omits_env_when_unassigned()
    {
        var id = Guid.NewGuid();
        var registration = Reg(id);

        ConfigScope.ResolutionOrder(registration).Should().Equal($"host:{id}", "");
    }

    [Fact]
    public void ResolutionOrder_orders_tags_deterministically_regardless_of_insertion_order()
    {
        var id = Guid.NewGuid();
        var registration = Reg(id, tags: [("zone", "z1"), ("rack", "r1")]);

        ConfigScope.ResolutionOrder(registration).Should().Equal(
            $"host:{id}", "tag:rack=r1", "tag:zone=z1", "");
    }

    [Fact]
    public void Matches_is_true_only_for_a_scope_the_component_selects()
    {
        var id = Guid.NewGuid();
        var registration = Reg(id, "prod", ("rack", "r1"));

        ConfigScope.Matches(ConfigScope.Default, registration).Should().BeTrue();
        ConfigScope.Matches("env:prod", registration).Should().BeTrue();
        ConfigScope.Matches("tag:rack=r1", registration).Should().BeTrue();
        ConfigScope.Matches($"host:{id}", registration).Should().BeTrue();

        ConfigScope.Matches("env:edge", registration).Should().BeFalse();
        ConfigScope.Matches("tag:rack=r2", registration).Should().BeFalse();
        ConfigScope.Matches($"host:{Guid.NewGuid()}", registration).Should().BeFalse();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("env:prod", true)]
    [InlineData("tag:rack=r1", true)]
    [InlineData("tag:rack=", true)]   // empty value is allowed
    [InlineData("host:00000000-0000-0000-0000-000000000000", true)]
    [InlineData("env:", false)]        // empty environment
    [InlineData("env:   ", false)]     // whitespace-only environment
    [InlineData("tag:=r1", false)]     // empty key
    [InlineData("tag:  =r1", false)]   // whitespace-only key
    [InlineData("tag:rack", false)]    // missing '='
    [InlineData("host:not-a-guid", false)]
    [InlineData("bogus", false)]       // unknown prefix
    public void IsValid_accepts_well_formed_selectors_only(string scope, bool expected)
    {
        ConfigScope.IsValid(scope).Should().Be(expected);
    }

    [Fact]
    public void TryCanonicalize_lower_cases_and_trims_an_environment_scope()
    {
        var ok = ConfigScope.TryCanonicalize("env:Prod", out var canonical, out var error);

        ok.Should().BeTrue();
        canonical.Should().Be("env:prod");
        error.Should().BeNull();
    }

    [Fact]
    public void TryCanonicalize_trims_surrounding_whitespace()
    {
        var ok = ConfigScope.TryCanonicalize("  env: Prod  ", out var canonical, out _);

        ok.Should().BeTrue();
        canonical.Should().Be("env:prod");
    }

    [Theory]
    [InlineData("host:0F8FAD5B-D9CB-469F-A165-70867728950E")]
    [InlineData("host:{0F8FAD5B-D9CB-469F-A165-70867728950E}")]
    [InlineData("host:0F8FAD5BD9CB469FA16570867728950E")]
    public void TryCanonicalize_normalizes_any_guid_format_to_the_lower_case_D_form(string scope)
    {
        var ok = ConfigScope.TryCanonicalize(scope, out var canonical, out _);

        ok.Should().BeTrue();
        canonical.Should().Be("host:0f8fad5b-d9cb-469f-a165-70867728950e");
    }

    [Fact]
    public void TryCanonicalize_lower_cases_a_tag_key_and_value()
    {
        var ok = ConfigScope.TryCanonicalize("tag:Rack=R1", out var canonical, out _);

        ok.Should().BeTrue();
        canonical.Should().Be("tag:rack=r1");
    }

    [Fact]
    public void TryCanonicalize_splits_a_tag_on_the_first_equals_sign()
    {
        var ok = ConfigScope.TryCanonicalize("tag:a=b=c", out var canonical, out _);

        ok.Should().BeTrue();
        canonical.Should().Be("tag:a=b=c");
    }

    [Theory]
    [InlineData("a=b", false)]
    [InlineData("a:b", false)]
    [InlineData("a b", false)]
    [InlineData("rack", true)]
    public void IsValidTagKey_rejects_delimiters_and_whitespace(string key, bool expected)
    {
        ConfigScope.IsValidTagKey(key, out _).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void TryCanonicalize_treats_an_empty_or_null_scope_as_the_default(string? scope)
    {
        var ok = ConfigScope.TryCanonicalize(scope, out var canonical, out var error);

        ok.Should().BeTrue();
        canonical.Should().Be("");
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("host:not-a-guid")]
    public void TryCanonicalize_reports_an_error_for_a_malformed_scope(string scope)
    {
        var ok = ConfigScope.TryCanonicalize(scope, out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void ForEnvironment_and_ForTag_produce_the_canonical_form()
    {
        ConfigScope.ForEnvironment("Prod").Should().Be("env:prod");
        ConfigScope.ForTag("Rack", "R1").Should().Be("tag:rack=r1");
    }

    [Fact]
    public void TryCanonicalize_rejects_an_environment_scope_exceeding_the_max_length()
    {
        var ok = ConfigScope.TryCanonicalize("env:" + new string('a', 300), out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().Contain(ConfigScope.MaxLength.ToString());
    }

    [Fact]
    public void TryCanonicalize_rejects_a_tag_scope_exceeding_the_max_length()
    {
        var ok = ConfigScope.TryCanonicalize("tag:k=" + new string('b', 300), out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().Contain(ConfigScope.MaxLength.ToString());
    }
}
