using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class CreateCatletSagaData : TaskWorkflowSagaData
{
    public CatletConfig? Config { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public string? AgentName { get; set; }

    public CreateCatletSagaState State { get; set; }
        
    public Guid TenantId { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> PendingGenes { get; set; } = [];
}
