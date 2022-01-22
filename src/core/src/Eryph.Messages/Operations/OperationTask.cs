

using System;

namespace Eryph.Messages.Operations
{
    public class OperationTask<T> : IOperationTaskMessage where T : class, new()
    {
        public OperationTask(T command, Guid operationId, Guid taskId)
        {
            Command = command;
            OperationId = operationId;
            TaskId = taskId;
        }

        public T Command { get; }
        public Guid OperationId { get; }
        public Guid TaskId { get; }
    }
}