using System;
using Haipa.Messages.Operations;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMAgent)]
    public class StartMachineCommand : OperationTaskCommand, IMachineCommand
    {
        public Guid MachineId { get; set; }
    }
}