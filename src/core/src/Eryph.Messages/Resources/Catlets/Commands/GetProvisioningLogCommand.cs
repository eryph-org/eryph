using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class GetProvisioningLogCommand : IHasResource, ICommandWithName
{
    public Guid CatletId { get; set; }
    public string GetCommandName() => "Reading provisioning log";
    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
