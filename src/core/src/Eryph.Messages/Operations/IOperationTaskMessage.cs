using System;

namespace Eryph.Messages.Operations
{
    /// <summary>
    ///     Interface for all Messages for operation tasks
    /// </summary>
    public interface IOperationTaskMessage
    {
        Guid OperationId { get; }

        Guid InitiatingTaskId { get; }

        Guid TaskId { get;  }
    }

}