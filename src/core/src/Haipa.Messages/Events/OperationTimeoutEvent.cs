using System;

namespace Haipa.Messages.Operations
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class OperationTimeoutEvent
    {
        public Guid OperationId { get; set; }
    }

}