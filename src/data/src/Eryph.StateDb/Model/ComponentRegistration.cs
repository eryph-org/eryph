using System;
using System.Collections.Generic;
using System.Text.Json;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// The controller's record of a component that has registered as part of the
/// deployment — the durable, cross-process service catalog. Identity is the
/// stable <see cref="ComponentId"/> (not the machine name). Applied config
/// versions are stored as a JSON column following the same pattern as
/// <see cref="CatletMetadata"/>.
/// </summary>
public class ComponentRegistration
{
    public Guid Id { get; set; }

    public Guid ComponentId { get; set; }

    public ComponentType ComponentType { get; set; }

    public Guid InstanceId { get; set; }

    public required string MachineName { get; set; }

    public string? Version { get; set; }

    public required string InboundQueue { get; set; }

    public ComponentRegistrationStatus Status { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset LastHeartbeat { get; set; }

    internal string AppliedConfigVersionsJson
    {
        get => JsonSerializer.Serialize(AppliedConfigVersions);
        set => AppliedConfigVersions = DeserializeOrEmpty<Dictionary<ConfigDomain, Dictionary<string, long>>>(value);
    }

    // Tolerate content that no longer parses (e.g. a renamed/removed ConfigDomain key from an older build,
    // or a corrupted value): treat it as empty rather than throwing, which would poison every read of this
    // registration (heartbeat, upsert) and wedge the component out of the catalog.
    private static T DeserializeOrEmpty<T>(string? json)
        where T : new()
    {
        if (string.IsNullOrEmpty(json))
            return new();
        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>The config version this component has applied, per domain and scope.</summary>
    public Dictionary<ConfigDomain, Dictionary<string, long>> AppliedConfigVersions { get; set; } = new();

    /// <summary>The applied version for a (domain, scope), or 0 when none was applied.</summary>
    public long GetAppliedVersion(ConfigDomain domain, string scope) =>
        AppliedConfigVersions.TryGetValue(domain, out var byScope)
        && byScope.TryGetValue(scope, out var version)
            ? version
            : 0;

    /// <summary>Records an applied version for a (domain, scope), keeping the highest seen.</summary>
    public void SetAppliedVersion(ConfigDomain domain, string scope, long version)
    {
        if (!AppliedConfigVersions.TryGetValue(domain, out var byScope))
            AppliedConfigVersions[domain] = byScope = new Dictionary<string, long>();
        byScope[scope] = byScope.TryGetValue(scope, out var existing) && existing > version
            ? existing
            : version;
    }

    /// <summary>Replaces the applied versions from a component's reported set.</summary>
    public void SetAppliedVersions(IEnumerable<AppliedConfigVersion> versions)
    {
        AppliedConfigVersions = new();
        foreach (var version in versions)
            SetAppliedVersion(version.Domain, version.Scope, version.Version);
    }

    internal string AdvertisedEndpointsJson
    {
        get => JsonSerializer.Serialize(AdvertisedEndpoints);
        set => AdvertisedEndpoints = DeserializeOrEmpty<Dictionary<string, string>>(value);
    }

    /// <summary>Service endpoints this component hosts and advertises (logical name → URL).</summary>
    public Dictionary<string, string> AdvertisedEndpoints { get; set; } = new();

    /// <summary>Operator-assigned environment used to target scoped configuration; null if unassigned.
    /// Operator-owned metadata (set via the management API), not reported by the component.</summary>
    public string? Environment { get; set; }

    internal string TagsJson
    {
        get => JsonSerializer.Serialize(Tags);
        set => Tags = DeserializeOrEmpty<Dictionary<string, string>>(value);
    }

    /// <summary>Operator-assigned tags (key → value) used to target scoped configuration.
    /// Operator-owned metadata, not reported by the component.</summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}
