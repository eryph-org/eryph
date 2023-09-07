using System;

namespace Eryph.Messages.Resources.Catlets.Events;

[SubscribesMessage(MessageSubscriber.Controllers)]
public class CatletUpTimeChangedEvent
{
    public Guid VmId { get; set; }
    public TimeSpan UpTime { get; set; }
}