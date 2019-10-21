using System;

namespace Haipa.Messages.Operations
{
    [SubscribeEvent(MessageSubscribers.ControllerModules)]
    [Message(MessageOwner.TaskQueue)]
    public class OperationTimeoutEvent
    {
        public Guid OperationId { get; set; }
    }

}