using System;

namespace Eryph.Messages.Operations.Commands
{

    /// <summary>
    /// Generic message that wraps a task message
    /// </summary>
    public class OperationTaskSystemMessage<TMessage> : IOperationTaskMessage
    {

        // ReSharper disable once UnusedMember.Global
        public OperationTaskSystemMessage()
        {
        }

        public OperationTaskSystemMessage(TMessage message, Guid operationId, Guid initiatingTaskId, Guid taskId)
        {
            Message = message;
            OperationId = operationId;
            InitiatingTaskId = initiatingTaskId;
            TaskId = taskId;
        }

        public TMessage Message { get; set; }

        public Guid OperationId { get; set; }
        public Guid InitiatingTaskId { get; set; }


        public Guid TaskId { get; set; }
    }
}