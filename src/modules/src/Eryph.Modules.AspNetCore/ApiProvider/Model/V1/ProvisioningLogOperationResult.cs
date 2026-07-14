using System;
using System.Collections.Generic;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

/// <summary>
/// The result of reading a catlet's provisioning log. Carries both a rendered,
/// human-readable text log and the reassembled raw events.
/// </summary>
public class ProvisioningLogOperationResult : OperationResult
{
    /// <summary>
    /// The provisioning log rendered as human-readable text.
    /// </summary>
    public string RenderedLog { get; set; } = "";

    /// <summary>
    /// The reassembled provisioning events, ordered by time.
    /// </summary>
    public IReadOnlyList<ProvisioningLogEntry> Events { get; set; } = new List<ProvisioningLogEntry>();
}

/// <summary>
/// One decoded cloud-init provisioning event (split messages already merged).
/// </summary>
public class ProvisioningLogEntry
{
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
    /// The event message (module outcome or failure reason), if any.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The timestamp of the event, if it could be parsed.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
}
