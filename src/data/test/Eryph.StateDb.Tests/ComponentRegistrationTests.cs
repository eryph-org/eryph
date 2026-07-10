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
    public void AppliedConfigVersions_round_trips_the_nested_shape()
    {
        var registration = Empty();
        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "", 5);
        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "env:prod", 2);

        var reloaded = Empty();
        reloaded.AppliedConfigVersionsJson = registration.AppliedConfigVersionsJson;

        reloaded.GetAppliedVersion(ConfigDomain.StorageConfig, "").Should().Be(5);
        reloaded.GetAppliedVersion(ConfigDomain.StorageConfig, "env:prod").Should().Be(2);
    }

    [Fact]
    public void AppliedConfigVersions_migrates_the_legacy_flat_shape_into_the_default_scope()
    {
        // The column once stored Dictionary<ConfigDomain, long>; a value predating the per-scope shape
        // must be migrated into the default scope, not dropped (dropping would trigger a fleet-wide
        // re-push on upgrade).
        var registration = Empty();

        registration.AppliedConfigVersionsJson = """{"StorageConfig":5,"Endpoints":9}""";

        registration.GetAppliedVersion(ConfigDomain.StorageConfig, "").Should().Be(5);
        registration.GetAppliedVersion(ConfigDomain.Endpoints, "").Should().Be(9);
    }

    [Fact]
    public void AppliedConfigVersions_falls_back_to_empty_for_unparseable_content()
    {
        var registration = Empty();

        registration.AppliedConfigVersionsJson = "not json";

        registration.AppliedConfigVersions.Should().BeEmpty();
    }
}
