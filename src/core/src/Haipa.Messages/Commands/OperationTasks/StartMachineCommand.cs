using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StartMachineCommand : OperationTaskCommand, IResourceCommand
    {
        public Resource Resource { get; set; }
    }

    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class StartVMCommand : OperationTaskCommand, IVMCommand
    {
        public long MachineId { get; set; }
        public Guid VMId { get; set; }
    }
}