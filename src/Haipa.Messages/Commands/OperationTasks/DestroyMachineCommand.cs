using Haipa.Messages.Operations;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands.OperationTasks
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyResourcesCommand : OperationTaskCommand, IResourcesCommand
    {
        public Resource[] Resources { get; set; }
    }

    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyMachineCommand : OperationTaskCommand, IResourceCommand
    {
        public Resource Resource { get; set; }
    }

    public class DestroyResourcesResponse
    {
        public Resource[] DestroyedResources { get; set; }
        public Resource[] DetachedResources { get; set; }
    }
}