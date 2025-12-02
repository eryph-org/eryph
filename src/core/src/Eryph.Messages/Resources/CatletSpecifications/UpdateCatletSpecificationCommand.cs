using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Eryph.Core.Genetics;
using Eryph.Resources;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletSpecificationCommand : IHasCorrelationId, IHasResource
{
    public Guid SpecificationId { get; set; }

    public string ContentType { get; set; }

    public string Configuration { get; set; }

    public ISet<Architecture> Architectures { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public Resource Resource => new(ResourceType.CatletSpecification, SpecificationId);

    public Guid CorrelationId { get; set; }
}
