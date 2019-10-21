using System;
using Haipa.Messages.Operations;

namespace Haipa.Messages.Commands.OperationTasks
{
    [Message(MessageOwner.VMAgent)]
    public class StopMachineCommand : OperationTaskCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }
}