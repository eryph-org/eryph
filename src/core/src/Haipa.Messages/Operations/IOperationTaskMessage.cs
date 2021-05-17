using System;

namespace Haipa.Messages.Operations
{
    /// <summary>
    ///     Interface for all Messages for operation tasks
    /// </summary>
    public interface IOperationTaskMessage
    {
        Guid OperationId { get; }
        Guid TaskId { get;  }
    }

}