using Eryph.Core;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Holds the most recently applied controller-distributed <see cref="PlacementConfig"/>
/// — the datastore/environment name vocabulary the agent is allowed to serve — so the
/// provisioning handlers can enforce it. Updated by <see cref="PlacementConfigRealizer"/>.
/// </summary>
internal interface IPlacementConfigProvider
{
    /// <summary>The last applied placement configuration; empty until the first apply.</summary>
    PlacementConfig Current { get; }

    void Update(PlacementConfig config);
}

internal sealed class PlacementConfigProvider : IPlacementConfigProvider
{
    private volatile PlacementConfig _current = new();

    public PlacementConfig Current => _current;

    public void Update(PlacementConfig config) => _current = config ?? new PlacementConfig();
}
