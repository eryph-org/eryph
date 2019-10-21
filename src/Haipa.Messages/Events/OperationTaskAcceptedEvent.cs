using System;

namespace Haipa.Messages.Operations
{
    [Message(MessageOwner.TaskQueue)]
    [SubscribeEvent(MessageSubscribers.ControllerModules)]
    public class OperationTaskAcceptedEvent : IOperationTaskMessage
    {
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
        public string AgentName { get; set; }

    }
}