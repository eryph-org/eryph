using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyCatletCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }
    public string GetCommandName() => "Destroy Catlet";
    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
