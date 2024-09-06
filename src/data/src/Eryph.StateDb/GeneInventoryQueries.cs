using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

internal class GeneInventoryQueries(
    StateStoreContext dbContext)
    : IGeneInventoryQueries
{
    public Task<List<Gene>> FindUnusedGenes(string agentName) =>
        CreateUnusedGenesQuery()
            .Where(g => g.LastSeenAgent == agentName)
            .ToListAsync();

    public Task<bool> IsUnusedGene(Guid geneId) =>
        CreateUnusedGenesQuery()
            .Where(g => g.Id == geneId)
            .AnyAsync();

    public Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default) =>
        dbContext.Catlets
            .Where(c => c.AgentName == agentName
                        && dbContext.MetadataGenes
                            .Any(mg => mg.GeneId == geneId.Value
                                       && c.MetadataId == mg.MetadataId))
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default) =>
        dbContext.VirtualDisks
            .Where(d => d.StorageIdentifier == geneId.Value
                        && d.LastSeenAgent == agentName)
            .SelectMany(d => d.Children)
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Gene> CreateUnusedGenesQuery() =>
        from gene in dbContext.Genes
        where gene.GeneType != GeneType.Volume
              || !dbContext.VirtualDisks.Any(d => d.StorageIdentifier == gene.GeneId
                                                  && d.LastSeenAgent == gene.LastSeenAgent
                                                  && (d.AttachedDrives.Count == 0 || d.Children.Count == 0))
        where gene.GeneType != GeneType.Fodder
              || !dbContext.MetadataGenes.Any(mg => mg.GeneId == gene.GeneId)
        select gene;
}
