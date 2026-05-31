using System;

namespace Eryph.Messages.Components;

/// <summary>
/// The controller's acknowledgement of a registration, routed directly to the
/// component's inbound queue. Confirms the component id the controller recorded.
/// </summary>
public class ComponentRegisteredEvent
{
    public Guid ComponentId { get; set; }
}
