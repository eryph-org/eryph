using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.CatletSpecifications;

[SendMessageTo(MessageRecipient.GenePoolAgent)]
public class BuildCatletSpecificationGenePoolCommand : IGenePoolAgentCommand, ICommandWithName
{
    public CatletConfig? CatletConfig { get; set; }

    public Architecture? CatletArchitecture { get; set; }

    public string GetCommandName() => "Build catlet specification";

    public string? AgentName { get; set; }
}
