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

    public long Version { get; set; }

    public string Payload { get; set; } = "";
}
