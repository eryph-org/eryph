using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSagaData
{
    public UpdateCatletSagaState State { get; set; }

    public CatletConfig? Config { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier>? ResolvedGenes { get; set; }

    public Guid CatletId { get; set; }
        
    public string? AgentName { get; set; }
        
    public Guid ProjectId { get; set; }
        
    public Guid TenantId { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> PendingGenes { get; set; } = [];
}
