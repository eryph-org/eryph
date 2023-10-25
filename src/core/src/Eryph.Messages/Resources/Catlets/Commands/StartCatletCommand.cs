using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StartCatletCommand : IHasResource, ICommandWithName
    {
        public Guid CatletId { get; set; }
        public Resource Resource => new(ResourceType.Catlet, CatletId);
        public string GetCommandName()
        {
            return "Starting Catlet";
        }
    }
}