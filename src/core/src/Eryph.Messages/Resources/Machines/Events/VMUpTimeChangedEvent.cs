using System;

namespace Eryph.Messages.Resources.Machines.Events;

[SubscribesMessage(MessageSubscriber.Controllers)]
public class VMUpTimeChangedEvent
{
    public Guid VmId { get; set; }
    public TimeSpan UpTime { get; set; }
}