namespace Eryph.Core.Settings;

/// <summary>
/// Controller-owned settings — the controller-scoped counterpart to the agent's
/// <c>agentsettings.yml</c>. Stored as a single YAML file, organized into sections.
/// Currently only <see cref="Placement"/> (the datastore/environment name catalog
/// the controller owns and distributes to agents). New controller settings are
/// added as further sections.
/// </summary>
public sealed class ControllerSettings
{
    public PlacementConfig Placement { get; set; } = new();
}
