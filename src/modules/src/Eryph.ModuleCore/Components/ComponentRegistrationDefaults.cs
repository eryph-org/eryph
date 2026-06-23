using System;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Shared timing for component liveness: a single source for the heartbeat interval (used by the
/// component-side <see cref="ComponentHeartbeatService"/>) and the controller-side timeout after
/// which a silent component is no longer considered live.
/// </summary>
public static class ComponentRegistrationDefaults
{
    /// <summary>How often a registered component sends a heartbeat to the controller.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long after its last heartbeat a component is still treated as live. Set to three missed
    /// <see cref="HeartbeatInterval"/>s so a single dropped heartbeat (or a brief delay) does not flap
    /// a healthy component out of the active set.
    /// </summary>
    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(90);
}
