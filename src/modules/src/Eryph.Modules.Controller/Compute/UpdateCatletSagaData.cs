using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using LanguageExt;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSagaData
{
    public UpdateVMState State { get; set; }

    public CatletConfig? Config { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public Guid CatletId { get; set; }
        
    public string? AgentName { get; set; }
        
    public Guid ProjectId { get; set; }
        
    public Guid TenantId { get; set; }

    /// <summary>
    /// Use <see cref="SetPendingGenes"/>, <see cref="RemovePendingGene"/>
    /// and <see cref="HasPendingGenes"/> instead.
    /// </summary>
    /// <remarks>
    /// Rebus uses a hard-coded Newtonsoft.Json serializer. Hence,
    /// we cannot directly store <see cref="GeneIdentifierWithType"/>.
    /// </remarks>
    public List<GeneIdentifierWithType> PendingGenes { get; set; } = [];
}
