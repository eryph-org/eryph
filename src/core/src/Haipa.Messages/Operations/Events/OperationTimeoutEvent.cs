using System;

namespace Haipa.Messages.Operations.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class OperationTimeoutEvent
    {
        public Guid OperationId { get; set; }
    }
}