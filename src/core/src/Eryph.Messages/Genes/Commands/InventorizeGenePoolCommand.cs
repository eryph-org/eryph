using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenePoolAgent)]
public class InventorizeGenePoolCommand : IGenePoolAgentCommand
{
    public string AgentName { get; set; }
}
