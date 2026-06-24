using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class RemoveGeneSagaData
{
    public string AgentName { get; set; } = null!;

    public UniqueGeneIdentifier GeneId { get; set; } = null!;
}
