using System;

namespace Eryph.Resources.Machines;

/// <summary>
/// One decoded cloud-init provisioning event, reassembled from the guest's
/// <c>CLOUD_INIT|…</c> Hyper-V KVP entries (split messages already merged).
/// </summary>
public class ProvisioningLogEvent
{
    /// <summary>
    /// The cloud-init incarnation (boot time as Unix epoch seconds) the event
    /// belongs to. Events of a single boot share the same incarnation.
    /// </summary>
    public long Incarnation { get; set; }

    /// <summary>
    /// The stage or <c>&lt;stage&gt;/&lt;module&gt;</c> name of the event.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The event type: <c>start</c> or <c>finish</c>.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// The result of a <c>finish</c> event: <c>SUCCESS</c> or <c>FAIL</c>.
    /// Null for <c>start</c> events.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// The event message (the module outcome or a failure reason). Null when the
    /// event carries no message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The timestamp of the event. Null when it could not be parsed.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
}
