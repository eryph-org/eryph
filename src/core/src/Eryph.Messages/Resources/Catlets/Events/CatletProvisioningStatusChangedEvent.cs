using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Events;

/// <summary>
/// Raised by the agent's provisioning monitor when the provisioning status of a
/// catlet (read from guest-services) changes during its first boot. The
/// controller resolves the catlet by <see cref="VmId"/> and persists the status.
/// </summary>
[SubscribesMessage(MessageSubscriber.Controllers)]
public class CatletProvisioningStatusChangedEvent
{
    public Guid VmId { get; set; }

    public ProvisioningStatus Status { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
