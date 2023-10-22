using Eryph.ConfigModel;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class PrepareGeneCommand : IHostAgentCommand, ICommandWithName
{
    public GeneType GeneType { get; set; }

    public string GeneName { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    public string GetCommandName()
    {
        var name = $"{GeneType} {GeneName}";
        return $"Preparing {name}";
    }
}