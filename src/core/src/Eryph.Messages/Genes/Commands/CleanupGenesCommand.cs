using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CleanupGenesCommand : ICommandWithName
{
    public string GetCommandName() => "Cleanup gene pool";
}
