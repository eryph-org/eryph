using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class StopCatletCommand : IHasResource, ICommandWithName
    {
        public Guid CatletId { get; set; }
        public Resource Resource => new(ResourceType.Catlet, CatletId);
        public bool Graceful { get; set; }
        public string GetCommandName()
        {
            return "Stopping Catlet";
        }
    }
}