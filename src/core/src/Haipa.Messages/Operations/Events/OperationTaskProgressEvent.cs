using System;

namespace Haipa.Messages.Operations.Events
{
    [SubscribesMessage(MessageSubscriber.Controllers)]
    public class OperationTaskProgressEvent
    {
        public Guid Id { get; set; }

        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
        public string Message { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

}