using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class PrepareGeneCommand : IHostAgentCommand, ICommandWithName
{
    public UniqueGeneIdentifier Gene { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    public string GetCommandName() => $"Preparing gene {Gene}";
}
