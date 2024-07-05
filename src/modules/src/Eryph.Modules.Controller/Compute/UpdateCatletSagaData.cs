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

    public bool Updated;

    public bool Validated;

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
    public List<PendingGene>? PendingGenes { get; set; }

    public class PendingGene
    {
        public required GeneType GeneType { get; init; }

        public required string GeneIdentifier { get; init; }
    }

    public void SetPendingGenes(Seq<GeneIdentifierWithType> pendingGenes)
    {
        PendingGenes = pendingGenes.Map(g => new PendingGene
        {
            GeneType = g.GeneType,
            GeneIdentifier = g.GeneIdentifier.Value
        }).ToList();
    }

    public void RemovePendingGene(GeneIdentifierWithType gene)
    {
        SetPendingGenes(PendingGenes.ToSeq()
            .Map(g => new GeneIdentifierWithType(
                g.GeneType,
                GeneIdentifier.New(g.GeneIdentifier)))
            .Filter(g => g != gene));
    }

    public bool HasPendingGenes() => PendingGenes?.Count > 0;
}
