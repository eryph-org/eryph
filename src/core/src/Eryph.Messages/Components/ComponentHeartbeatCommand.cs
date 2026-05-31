using System;
using System.Collections.Generic;

namespace Eryph.Messages.Components;

/// <summary>
/// Periodic liveness signal from a component. Echoes the configuration versions
/// the component currently has applied so the controller can detect drift and
/// re-push, and refresh the registration's last-seen timestamp.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class ComponentHeartbeatCommand
{
    public Guid ComponentId { get; set; }

    public Guid InstanceId { get; set; }

    public Dictionary<ConfigDomain, long> AppliedConfigVersions { get; set; } = new();

    public DateTimeOffset Timestamp { get; set; }
}
