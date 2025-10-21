using Eryph.Resources;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateCatletSpecificationCommand : ICommandWithName, IHasCorrelationId
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string ConfigYaml { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => $"Create catlet specification {Name}";
}
