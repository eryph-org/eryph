using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class PrepareGeneCommand : IHostAgentCommand, ICommandWithName
{
    public GeneIdentifierWithType GeneIdentifier { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    public string GetCommandName() => $"Preparing {GeneIdentifier}";
}
