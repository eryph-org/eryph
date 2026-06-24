using System;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class RemoveGeneCommand : ICommandWithName
{
    public Guid Id { get; set; }

    public string GetCommandName() => "Remove gene from gene pool";
}
