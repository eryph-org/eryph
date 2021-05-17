using System;

namespace Haipa.Messages.Operations.Commands
{
    /// <inheritdoc />
    /// <summary>
    ///     Default base implementation of a task command.
    /// </summary>
    public abstract class OperationTaskCommand : IOperationTaskCommand
    {
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }
    }
}