using Haipa.Messages.Operations.Commands;
using Haipa.Resources;

namespace Haipa.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyResourcesCommand : OperationTaskCommand, IResourcesCommand
    {
        public Resource[] Resources { get; set; }
    }
}