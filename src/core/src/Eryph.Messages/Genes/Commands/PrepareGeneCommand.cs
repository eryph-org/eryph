using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenePoolAgent)]
public class PrepareGeneCommand : IGenePoolAgentCommand, ICommandWithName
{
    public UniqueGeneIdentifier Id { get; set; }

    public GeneHash Hash { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    public string GetCommandName() => $"Preparing gene {Id}";
}
