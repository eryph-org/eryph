using System.Collections.Generic;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Tracks, in memory, the configuration versions this component has applied per
/// (domain, scope). Reported in registration and heartbeats so the controller can
/// send only deltas and detect drift. Keyed by scope because each (domain, scope)
/// has an independent version counter — a component moved to a different scope must
/// not treat that scope's (possibly lower) version as already applied.
/// </summary>
public interface IComponentConfigState
{
    void SetApplied(ConfigDomain domain, string scope, long version);

    /// <summary>The version applied for a (domain, scope), or 0 when none was applied.</summary>
    long GetAppliedVersion(ConfigDomain domain, string scope);

    IReadOnlyList<AppliedConfigVersion> GetApplied();
}
