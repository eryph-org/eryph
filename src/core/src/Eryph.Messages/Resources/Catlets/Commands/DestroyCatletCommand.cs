using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class DestroyCatletCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }
    public Resource Resource => new(ResourceType.Catlet, CatletId);
    public string GetCommandName() => "Destroy Catlet";
}