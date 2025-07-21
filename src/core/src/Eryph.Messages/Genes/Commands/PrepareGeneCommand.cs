using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenepoolAgent)]
public class PrepareGeneCommand : IGenepoolAgentCommand, ICommandWithName
{
    public UniqueGeneIdentifier Gene { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    public string GetCommandName() => $"Preparing gene {Gene}";
}
