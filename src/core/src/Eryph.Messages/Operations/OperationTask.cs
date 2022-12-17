

using System;

namespace Eryph.Messages.Operations
{
    public class OperationTask<T> : IOperationTaskMessage where T : class, new()
    {
        public OperationTask(T command, Guid operationId, Guid initiatingTaskId, Guid taskId)
        {
            Command = command;
            OperationId = operationId;
            InitiatingTaskId = initiatingTaskId;
            TaskId = taskId;
        }

        public T Command { get; }
        public Guid OperationId { get; }
        public Guid InitiatingTaskId { get; }
        public Guid TaskId { get; }
    }
}