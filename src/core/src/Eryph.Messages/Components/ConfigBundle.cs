namespace Eryph.Messages.Components;

/// <summary>
/// A versioned snapshot of a single configuration domain. <see cref="Payload"/>
/// is the domain-specific serialized configuration (JSON); the receiving
/// component applies it through the matching realizer.
/// </summary>
public sealed class ConfigBundle
{
    public ConfigDomain Domain { get; set; }

    public long Version { get; set; }

    public string Payload { get; set; } = "";
}
