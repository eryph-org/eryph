using System;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletSpecificationCommand : ICommandWithName, IHasCorrelationId
{
    public string ConfigYaml { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => "Validate catlet specification";
}
