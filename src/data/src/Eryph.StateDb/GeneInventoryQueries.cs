using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb;

internal class GeneInventoryQueries(
    StateStoreContext dbContext)
    : IGeneInventoryQueries
{
    public Task<List<Gene>> FindUnusedGenes(
        string agentName,
        CancellationToken cancellationToken = default) =>
        CreateUnusedGenesQuery()
            .Where(g => g.LastSeenAgent == agentName)
            .ToListAsync(cancellationToken);

    public Task<bool> IsUnusedGene(
        Guid geneId,
        CancellationToken cancellationToken = default) =>
        CreateUnusedGenesQuery()
            .Where(g => g.Id == geneId)
            .AnyAsync(cancellationToken);

    public Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken = default) =>
        dbContext.Catlets
            .Where(c => c.AgentName == agentName
                        && dbContext.MetadataGenes
                            .Any(mg => mg.UniqueGeneIndex == uniqueGeneId.ToUniqueGeneIndex()
                                       && c.MetadataId == mg.MetadataId))
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken = default) =>
        dbContext.VirtualDisks
            .Where(d => d.UniqueGeneIndex == uniqueGeneId.ToUniqueGeneIndex()
                        && d.LastSeenAgent == agentName
                        && !d.Deleted)
            .SelectMany(d => d.Children)
            .Where(d => !d.Deleted)
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Gene> CreateUnusedGenesQuery() =>
        from gene in dbContext.Genes
        where gene.GeneType != GeneType.Volume
              || !dbContext.VirtualDisks.Any(d => d.UniqueGeneIndex == gene.UniqueGeneIndex
                                                  && d.LastSeenAgent == gene.LastSeenAgent
                                                  && !d.Deleted
                                                  && (d.AttachedDrives.Count != 0 || d.Children.Any(c => !c.Deleted)))
        where gene.GeneType != GeneType.Fodder
              || !dbContext.MetadataGenes.Any(mg => mg.UniqueGeneIndex == gene.UniqueGeneIndex)
        select gene;
}
