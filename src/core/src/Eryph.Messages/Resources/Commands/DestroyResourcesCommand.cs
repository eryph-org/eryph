using Eryph.Resources;

namespace Eryph.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyResourcesCommand : IGenericResourcesCommand, IHasResources
    {
        public Resource[] Resources { get; set; }
    }
}