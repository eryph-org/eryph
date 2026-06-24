using System;

namespace Eryph.Messages.Components;

/// <summary>
/// Sent by a component to the controller on graceful shutdown so its registration is removed
/// immediately, instead of waiting out the heartbeat timeout. Best-effort: if it does not arrive
/// (an unclean stop, or the bus is already shutting down) the liveness timeout still ages the
/// component out of the active set.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class DeregisterComponentCommand
{
    /// <summary>Stable identity of the component (not the machine name).</summary>
    public Guid ComponentId { get; set; }

    /// <summary>
    /// This run of the component. The controller deregisters only when it still matches the stored
    /// registration, so a late message from a previous instance cannot remove a restarted one.
    /// </summary>
    public Guid InstanceId { get; set; }
}
