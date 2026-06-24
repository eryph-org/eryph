using System;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class StopCatletCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }

    public CatletStopMode Mode { get; set; }

    public string GetCommandName()
    {
        return "Stopping Catlet";
    }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
