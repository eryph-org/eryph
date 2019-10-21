using System;
using Haipa.Messages.Operations;

namespace Haipa.Messages.Commands.OperationTasks
{
    [Message(MessageOwner.VMAgent)]
    [MachineMayNotExists]
    public class DestroyMachineCommand : OperationTaskCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }
}