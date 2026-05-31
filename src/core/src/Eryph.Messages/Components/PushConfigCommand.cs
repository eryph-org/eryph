using System;

namespace Eryph.Messages.Components;

/// <summary>
/// A targeted push of a single updated configuration bundle to one live
/// component, routed directly to its inbound queue. Used by the controller when
/// a configuration domain changes.
/// </summary>
public class PushConfigCommand
{
    public Guid ComponentId { get; set; }

    public ConfigBundle Bundle { get; set; } = new();
}
