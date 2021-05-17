using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StopMachineCommand : OperationTaskCommand, IResourceCommand
    {
        public Resource Resource { get; set; }
    }


    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class StopVMCommand : OperationTaskCommand, IVMCommand
    {
        public long MachineId { get; set; }
        public Guid VMId { get; set; }
    }
}