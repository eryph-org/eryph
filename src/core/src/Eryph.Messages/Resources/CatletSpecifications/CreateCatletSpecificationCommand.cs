using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateCatletSpecificationCommand : ICommandWithName, IHasCorrelationId
{
    public Guid ProjectId { get; set; }

    public string ContentType { get; set; }

    public string Configuration { get; set; }

    public ISet<Architecture> Architectures { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => "Create catlet specification";
}
