using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class GetGuestServicesStatusCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }
    public string GetCommandName() => "Reading guest services status";
    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
