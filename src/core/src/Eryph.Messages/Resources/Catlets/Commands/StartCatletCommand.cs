using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StartCatletCommand : IResourceCommand
    {
        public Resource Resource { get; set; }
    }
}