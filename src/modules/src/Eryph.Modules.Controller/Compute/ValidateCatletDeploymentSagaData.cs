using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class ValidateCatletDeploymentSagaData
{
    public ValidateCatletDeploymentSagaState State { get; set; }

    public Guid TenantId { get; set; }

    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = [];

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> PendingGenes { get; set; } = [];
}
