using System;

namespace Eryph.Messages.Resources.Machines.Events;

[SubscribesMessage(MessageSubscriber.Controllers)]
public class VCatletUpTimeChangedEvent
{
    public Guid VmId { get; set; }
    public TimeSpan UpTime { get; set; }
}