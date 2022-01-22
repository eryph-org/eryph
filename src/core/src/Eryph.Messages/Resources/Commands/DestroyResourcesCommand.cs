using Eryph.Messages.Operations.Commands;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyResourcesCommand : IResourcesCommand
    {
        public Resource[] Resources { get; set; }
    }
}