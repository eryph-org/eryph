using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeleteCatletSpecificationCommand : ICommandWithName, IHasResource
{
    public Guid SpecificationId { get; set; }

    public Resource Resource => new(ResourceType.CatletSpecification, SpecificationId);

    public string GetCommandName() => "Delete catlet specification";
}
