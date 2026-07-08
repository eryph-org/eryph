using System;

namespace Eryph.Messages.Components;

/// <summary>
/// A component's acknowledgement that it applied (or failed to apply) a
/// configuration bundle. The controller records the applied version on the
/// registration monotonically per (domain, scope) (a lower or equal
/// <see cref="Version"/> for the same <see cref="Scope"/> is ignored), so a late
/// ack cannot regress newer state. <see cref="Timestamp"/> is informational only.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class ConfigAppliedEvent
{
    public Guid ComponentId { get; set; }

    public ConfigDomain Domain { get; set; }

    /// <summary>The scope the acknowledged bundle was resolved for (empty = default).</summary>
    public string Scope { get; set; } = "";

    public long Version { get; set; }

    public bool Success { get; set; }

    public string Error { get; set; } = "";

    public DateTimeOffset Timestamp { get; set; }
}
