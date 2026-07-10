using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Tests;

/// <summary>
/// Verifies the JSON-column (de)serialization of <see cref="ComponentRegistration.AppliedConfigVersions"/>,
/// including the migration of the legacy flat shape and tolerance of unparseable content.
/// </summary>
public class ComponentRegistrationTests
{
    private static ComponentRegistration Empty() => new()
    {
        MachineName = "host",
        InboundQueue = "q",
    };

    [Fact]
    public void AppliedConfigVersions_round_trips_the_scope_per_domain()
    {
        var registration = Empty();
        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "env:prod", 5);
        registration.SetAppliedVersion(ConfigDomain.Endpoints, "", 2);

        var reloaded = Empty();
        reloaded.AppliedConfigVersionsJson = registration.AppliedConfigVersionsJson;

        reloaded.GetAppliedVersion(ConfigDomain.StorageConfig, "env:prod").Should().Be(5);
        reloaded.GetAppliedVersion(ConfigDomain.Endpoints, "").Should().Be(2);
    }

    [Fact]
    public void SetAppliedVersion_keeps_one_effective_scope_per_domain()
    {
        // A domain has exactly one effective scope; applying a new scope replaces the previous one, so a
        // reverted scope is never reported as still-applied.
        var registration = Empty();
        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "", 5);

        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "env:prod", 1);

        registration.GetAppliedVersion(ConfigDomain.StorageConfig, "env:prod").Should().Be(1);
        registration.GetAppliedVersion(ConfigDomain.StorageConfig, "").Should().Be(0);
    }

    [Fact]
    public void AppliedConfigVersions_migrates_the_legacy_flat_shape_including_the_renamed_domain()
    {
        // The column once stored Dictionary<ConfigDomain, long> keyed by the OLD enum name
        // "PlacementConfig" (before it was renamed to StorageConfig). A real upgraded value must be
        // migrated — the flat version into the default scope, and the renamed key onto StorageConfig —
        // not dropped (dropping would trigger a fleet-wide re-push on upgrade).
        var registration = Empty();

        registration.AppliedConfigVersionsJson = """{"PlacementConfig":5,"Endpoints":9}""";

        registration.GetAppliedVersion(ConfigDomain.StorageConfig, "").Should().Be(5);
        registration.GetAppliedVersion(ConfigDomain.Endpoints, "").Should().Be(9);
    }

    [Fact]
    public void AppliedConfigVersions_skips_an_unknown_domain_key_but_keeps_the_rest()
    {
        var registration = Empty();

        registration.AppliedConfigVersionsJson = """{"SomeRemovedDomain":3,"Endpoints":9}""";

        registration.GetAppliedVersion(ConfigDomain.Endpoints, "").Should().Be(9);
        registration.AppliedConfigVersions.Should().ContainSingle();
    }

    [Fact]
    public void AppliedConfigVersions_falls_back_to_empty_for_unparseable_content()
    {
        var registration = Empty();

        registration.AppliedConfigVersionsJson = "not json";

        registration.AppliedConfigVersions.Should().BeEmpty();
    }
}
