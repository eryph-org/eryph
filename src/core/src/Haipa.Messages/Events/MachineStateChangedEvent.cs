using System;

namespace Haipa.Messages.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class MachineStateChangedEvent
    {
        public Guid MachineId { get; set; }
        public VmStatus Status { get; set; }
    }
}