using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenePoolAgent)]
public class PrepareGeneCommand : IGenePoolAgentCommand, ICommandWithName
{
    public UniqueGeneIdentifier? Id { get; set; }

    public GeneHash? Hash { get; set; }

    public string GetCommandName() => $"Preparing gene {Id}";

    [PrivateIdentifier] public string? AgentName { get; set; }
}
