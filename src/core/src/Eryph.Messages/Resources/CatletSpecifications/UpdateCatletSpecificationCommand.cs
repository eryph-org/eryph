using System;
using Eryph.Resources;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletSpecificationCommand : IHasCorrelationId, IHasResource
{
    public Guid SpecificationId { get; set; }

    public string ConfigYaml { get; set; }

    public Resource Resource => new(ResourceType.CatletSpecification, SpecificationId);

    public Guid CorrelationId { get; set; }
}
