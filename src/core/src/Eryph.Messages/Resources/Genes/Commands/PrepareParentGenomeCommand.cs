using Eryph.ConfigModel;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class PrepareParentGenomeCommand : IHostAgentCommand
{
    public string ParentName { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

}