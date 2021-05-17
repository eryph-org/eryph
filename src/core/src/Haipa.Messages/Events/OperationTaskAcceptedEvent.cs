using System;

namespace Haipa.Messages.Operations
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class OperationTaskAcceptedEvent : IOperationTaskMessage
    {
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
        public string AgentName { get; set; }

    }
}