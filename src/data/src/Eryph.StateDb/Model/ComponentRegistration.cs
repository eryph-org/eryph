using System;
using System.Collections.Generic;
using System.Linq;
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
        set => AppliedConfigVersions = DeserializeAppliedVersions(value);
    }

    // The value is stored keyed by ConfigDomain, which has been renamed (PlacementConfig → StorageConfig)
    // and reshaped (flat Dictionary&lt;ConfigDomain, long&gt; → per-scope nested) over this domain's history.
    // Parse it shape- and name-tolerantly so an upgraded value is MIGRATED rather than dropped: dropping
    // would make the controller re-push every domain to every component. Each entry is read independently:
    // - a numeric value is the legacy flat version, migrated into the default scope ("");
    // - an object value is the current per-scope map;
    // - the legacy "PlacementConfig" key is mapped to "StorageConfig";
    // - a genuinely unknown key or unparseable entry is skipped (not fatal to the rest).
    private static Dictionary<ConfigDomain, Dictionary<string, long>> DeserializeAppliedVersions(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new();

        Dictionary<string, JsonElement>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (JsonException)
        {
            return new();
        }
        if (raw is null)
            return new();

        var result = new Dictionary<ConfigDomain, Dictionary<string, long>>();
        foreach (var (rawKey, value) in raw)
        {
            var key = rawKey == "PlacementConfig" ? nameof(ConfigDomain.StorageConfig) : rawKey;
            if (!Enum.TryParse<ConfigDomain>(key, out var domain))
                continue;

            try
            {
                if (value.ValueKind == JsonValueKind.Number)
                    result[domain] = new Dictionary<string, long> { [""] = value.GetInt64() };
                else if (value.ValueKind == JsonValueKind.Object
                         && value.Deserialize<Dictionary<string, long>>() is { } byScope)
                    result[domain] = byScope;
            }
            catch (Exception)
            {
                // Skip this entry but keep the rest. Catch broadly (not just JsonException): GetInt64 on a
                // malformed/out-of-range number throws FormatException/InvalidOperationException, and the
                // whole point here is that no corrupt entry can wedge reads of the registration.
            }
        }

        return result;
    }

    // Tolerate content that no longer parses (e.g. a renamed/removed key from an older build, or a
    // corrupted value): treat it as empty rather than throwing, which would poison every read of this
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
        && byScope.TryGetValue(scope ?? "", out var version)
            ? version
            : 0;

    /// <summary>
    /// Records an applied version for a (domain, scope). A domain has exactly one effective scope, so a
    /// version for a DIFFERENT scope replaces the domain's entry; a version for the same scope keeps the
    /// highest seen. This mirrors the component's own one-scope-per-domain state, so a reverted scope is
    /// never treated as "already applied".
    /// </summary>
    public void SetAppliedVersion(ConfigDomain domain, string scope, long version)
    {
        scope ??= "";
        if (AppliedConfigVersions.TryGetValue(domain, out var byScope)
            && byScope.TryGetValue(scope, out var existing) && existing >= version)
            return;

        AppliedConfigVersions[domain] = new Dictionary<string, long> { [scope] = version };
    }

    /// <summary>Replaces the applied versions from a component's reported set (null entries skipped).</summary>
    public void SetAppliedVersions(IEnumerable<AppliedConfigVersion>? versions)
    {
        AppliedConfigVersions = new();
        foreach (var version in versions ?? [])
            if (version is not null)
                SetAppliedVersion(version.Domain, version.Scope ?? "", version.Version);
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

    /// <summary>The site the component is located in. Assigned the default site on first registration
    /// and operator-owned afterwards (re-registration must not overwrite it). Not nullable: a component
    /// always runs somewhere. Distinct from <see cref="Environment"/>, which is a single-valued
    /// config-targeting label and may legitimately be unassigned, whereas a site hosts many environments.</summary>
    public Guid SiteId { get; set; }

    internal string TagsJson
    {
        get => JsonSerializer.Serialize(Tags);
        // Coalesce null tag values to empty: a hand-edited/corrupt row can carry a JSON null (which the
        // non-null value annotation does not stop System.Text.Json from producing), and a null value
        // would NRE in scope resolution and poison every distribution read of this registration.
        set => Tags = DeserializeOrEmpty<Dictionary<string, string?>>(value)
            .ToDictionary(kv => kv.Key, kv => kv.Value ?? "");
    }

    /// <summary>Operator-assigned tags (key → value) used to target scoped configuration.
    /// Operator-owned metadata, not reported by the component.</summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}
