#nullable enable
using System.Collections.Generic;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

/// <summary>
/// The catlet's provisioning log, decoded from the guest's cloud-init KVP event
/// stream. Carries both a rendered, human-readable text log and the reassembled
/// raw events (split messages already merged).
/// </summary>
public class GetProvisioningLogVMCommandResponse
{
    /// <summary>
    /// The provisioning log rendered as human-readable text.
    /// </summary>
    public string RenderedLog { get; set; } = "";

    /// <summary>
    /// The reassembled provisioning events, ordered by time.
    /// </summary>
    public List<ProvisioningLogEvent> Events { get; set; } = new();
}
