using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class ValidateCatletDeploymentSagaData
{
    public ValidateCatletDeploymentSagaState State { get; set; }

    public string? AgentName { get; set; }

    public CatletConfig? Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } =
        new Dictionary<UniqueGeneIdentifier, GeneHash>();

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> PendingGenes { get; set; } =
        new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
