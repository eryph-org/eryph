using System.Collections.Generic;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Tracks, in memory, the configuration versions this component has applied per
/// domain. Reported in registration and heartbeats so the controller can send
/// only deltas and detect drift.
/// </summary>
public interface IComponentConfigState
{
    void SetApplied(ConfigDomain domain, long version);

    IReadOnlyDictionary<ConfigDomain, long> GetApplied();
}
