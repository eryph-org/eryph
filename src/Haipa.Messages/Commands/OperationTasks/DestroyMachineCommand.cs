using System;
using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    [ResourceMayNotExists]
    public class DestroyResourceCommand : OperationTaskCommand, IResourceCommand
    {
        public long ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }
    }

    [SendMessageTo(MessageRecipient.VMHostAgent)]
    [ResourceMayNotExists]
    public class DestroyVMCommand : OperationTaskCommand, IVMCommand
    {
        public Guid VMId { get; set; }
    }
}