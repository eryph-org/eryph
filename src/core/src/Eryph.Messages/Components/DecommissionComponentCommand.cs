using System;

namespace Eryph.Messages.Components;

/// <summary>
/// Sent by an operator/admin action to permanently decommission a component: the controller deletes
/// its broker user (the primary revocation — an immediate, hard bus cutoff independent of certificate
/// expiry) and removes its registration from the catalog. Unlike <see cref="DeregisterComponentCommand"/>
/// (a transient graceful-shutdown signal that a restart undoes), this is the explicit "gone for good"
/// action and is not instance-scoped.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class DecommissionComponentCommand
{
    /// <summary>Stable identity of the component to decommission (not the machine name).</summary>
    public Guid ComponentId { get; set; }
}
