using System;

namespace Haipa.Messages.Events
{
    [SubscribeEvent(MessageSubscribers.ControllerModules)]
    [Message(MessageOwner.TaskQueue)]
    public class MachineStateChangedEvent
    {
        public Guid MachineId { get; set; }
        public VmStatus Status { get; set; }
    }
}