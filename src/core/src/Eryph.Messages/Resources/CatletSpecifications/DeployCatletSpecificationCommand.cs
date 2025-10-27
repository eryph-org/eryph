using Eryph.Resources;
using System;
using System.Collections.Generic;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class DeployCatletSpecificationCommand : ICommandWithName, IHasCorrelationId, IHasResource
{
    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public Architecture Architecture { get; set; }

    public string Name { get; set; }

    public bool Redeploy { get; set; }

    public IReadOnlyDictionary<string, string> Variables { get; set; }

    public Guid CorrelationId { get; set; }

    public Resource Resource => new(ResourceType.CatletSpecification, SpecificationId);

    public string GetCommandName() => $"Deploy catlet specification {Name}";
}
