using System;

namespace Eryph.Messages.Components;

/// <summary>
/// A component's acknowledgement that it applied (or failed to apply) a
/// configuration bundle. The controller records the applied version on the
/// registration monotonically per domain (a lower or equal <see cref="Version"/>
/// is ignored), so a late ack cannot regress newer state. <see cref="Timestamp"/>
/// is informational only.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class ConfigAppliedEvent
{
    public Guid ComponentId { get; set; }

    public ConfigDomain Domain { get; set; }

    public long Version { get; set; }

    public bool Success { get; set; }

    public string Error { get; set; } = "";

    public DateTimeOffset Timestamp { get; set; }
}
