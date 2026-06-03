using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands
{
    [SendMessageTo(MessageRecipient.Controllers)]
    public class AddSshKeyCommand : IHasResource, ICommandWithName
    {
        public Guid CatletId { get; set; }
        public string SubjectId { get; set; }
        public string PublicKey { get; set; }
        public DateTimeOffset? KeyExpiry { get; set; }
        public Resource Resource => new(ResourceType.Catlet, CatletId);
        public string GetCommandName()
        {
            return "Adding SSH key";
        }
    }
}
