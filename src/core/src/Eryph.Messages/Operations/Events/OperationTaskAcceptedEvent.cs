using System;

namespace Eryph.Messages.Operations.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class OperationTaskAcceptedEvent : IOperationTaskMessage
    {
        public string AgentName { get; set; }
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
    }
}