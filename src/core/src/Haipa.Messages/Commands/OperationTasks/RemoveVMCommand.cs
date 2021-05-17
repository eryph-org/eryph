using System;
using Haipa.Messages.Operations;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class RemoveVMCommand : OperationTaskCommand, IVMCommand
    {
        public long MachineId { get; set; }
        public Guid VMId { get; set; }
    }
}