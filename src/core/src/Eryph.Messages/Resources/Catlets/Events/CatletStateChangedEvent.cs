using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.Controllers)]
public class CatletStateChangedEvent
{
    public Guid VmId { get; set; }

    public VmStatus Status { get; set; }

    public TimeSpan UpTime { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
