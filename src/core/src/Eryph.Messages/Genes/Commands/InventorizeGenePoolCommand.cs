using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenepoolAgent)]
public class InventorizeGenePoolCommand : IGenepoolAgentCommand
{
    public string AgentName { get; set; }
}
