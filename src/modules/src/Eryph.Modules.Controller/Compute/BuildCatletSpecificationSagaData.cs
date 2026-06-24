using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class BuildCatletSpecificationSagaData
{
    public BuildCatletSpecificationSagaState State { get; set; }

    public string? ContentType { get; set; }

    public string? Configuration { get; set; }

    public Architecture? Architecture { get; set; }

    public CatletConfig? ParsedConfig { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } =
        new Dictionary<UniqueGeneIdentifier, GeneHash>();

    // The agent which is hosting the gene pool
    public string? AgentName { get; set; }
}
