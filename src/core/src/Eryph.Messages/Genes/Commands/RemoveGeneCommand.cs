using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class RemoveGeneCommand : ICommandWithName
{
    public Guid Id { get; set; }

    public string GetCommandName() => $"Remove gene from gene pool";
}
