using Eryph.Resources;

namespace Eryph.Messages.Resources.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class DestroyResourcesCommand : IGenericResourcesCommand, IHasResources, ICommandWithName
    {
        public Resource[] Resources { get; set; }
        
        public string GetCommandName() => "Destroy Resources";
    }
}