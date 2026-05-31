namespace Eryph.Core;

/// <summary>
/// The controller-owned, operator-defined placement settings distributed to
/// agents: the cluster vocabulary of datastore and environment names. The agent
/// maps these names to concrete local paths itself (paths are agent-local); the
/// controller addresses placement by these abstract names.
/// </summary>
public sealed class PlacementConfig
{
    public string[] Datastores { get; set; } = [];

    public string[] Environments { get; set; } = [];
}
