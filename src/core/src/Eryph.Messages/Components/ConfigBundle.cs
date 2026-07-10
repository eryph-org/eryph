namespace Eryph.Messages.Components;

/// <summary>
/// A versioned snapshot of a single configuration domain. <see cref="Payload"/> is the
/// domain-specific serialized configuration — the format is per domain (e.g. JSON for
/// Endpoints, raw YAML for NetworkProviders); the receiving component applies it through
/// the matching realizer, which knows how to deserialize its own domain.
/// </summary>
public sealed class ConfigBundle
{
    public ConfigDomain Domain { get; set; }

    /// <summary>
    /// The scope selector this bundle was resolved for (empty = default). The receiving component tracks
    /// its applied version per (domain, scope) and echoes the scope in its acknowledgement, so a bundle
    /// resolved from a different scope (with an independent, possibly lower version counter) is not
    /// mistaken for one it already applied.
    /// </summary>
    public string Scope { get; set; } = "";

    public long Version { get; set; }

    public string Payload { get; set; } = "";
}
