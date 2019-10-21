using System;

namespace Haipa.Messages.Operations
{
    [SubscribeEvent(MessageSubscribers.ControllerModules)]
    
    public class OperationTaskProgressEvent
    {
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
        public string Message { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

}