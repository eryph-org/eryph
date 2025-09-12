using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSagaData
{
    public UpdateCatletSagaState State { get; set; }

    public CatletConfig? Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash>? ResolvedGenes { get; set; }

    public Guid CatletId { get; set; }
        
    public string? AgentName { get; set; }
        
    public Guid ProjectId { get; set; }

    public Architecture? Architecture { get; set; }
}
