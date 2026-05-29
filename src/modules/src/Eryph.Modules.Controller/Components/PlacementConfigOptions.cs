namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Options for the controller-owned placement configuration, bound from the
/// "Placement" configuration section.
/// </summary>
public sealed class PlacementConfigOptions
{
    /// <summary>
    /// Path to the operator-editable placement configuration JSON file. Empty or
    /// missing means an empty placement configuration.
    /// </summary>
    public string ConfigPath { get; set; } = "";
}
