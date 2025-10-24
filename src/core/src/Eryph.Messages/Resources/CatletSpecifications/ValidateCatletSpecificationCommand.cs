using System;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletSpecificationCommand : ICommandWithName, IHasCorrelationId
{
    public string ContentType { get; set; }

    public string Configuration { get; set; }

    public Architecture Architecture { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => "Validate catlet specification";
}
