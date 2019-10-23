using System;
using Haipa.Messages.Operations;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMAgent)]
    [MachineMayNotExists]
    public class DestroyMachineCommand : OperationTaskCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }
}