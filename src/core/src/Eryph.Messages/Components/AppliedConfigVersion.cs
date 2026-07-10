namespace Eryph.Messages.Components;

/// <summary>
/// A component's applied configuration version for one (<see cref="Domain"/>, <see cref="Scope"/>).
/// Versions are counted independently per scope, so the scope must be carried alongside the version:
/// a component moved to a different scope has a fresh (possibly lower) counter there, which a
/// scope-blind version comparison would misread as "already applied".
/// </summary>
public sealed class AppliedConfigVersion
{
    public ConfigDomain Domain { get; set; }

    /// <summary>The scope selector the version was applied for (empty = the default scope).</summary>
    public string Scope { get; set; } = "";

    public long Version { get; set; }
}
