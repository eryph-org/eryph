using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StopMachineCommand : OperationTaskCommand, IResourceCommand
    {
        public long ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }
    }


    [SendMessageTo(MessageRecipient.VMHostAgent)]
    public class StopVMCommand : OperationTaskCommand, IVMCommand
    {
        public Guid VMId { get; set; }
    }
}