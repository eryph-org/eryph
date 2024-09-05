using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

internal class GeneInventoryQueries(
    StateStoreContext dbContext)
    : IGeneInventoryQueries
{
    public Task<bool> IsUnusedFodderGene(Guid geneId)
    {
        return CreateUnusedFodderGenesQuery()
            .Where(g => g.Id == geneId)
            .AnyAsync();
    }

    public Task<bool> IsUnusedVolumeGene(Guid geneId)
    {
        return CreateUnusedVolumeGenesQuery()
            .Where(g => g.Id == geneId)
            .AnyAsync();
    }

    public Task<List<Gene>> GetUnusedFodderGenes(string agentName)
    {
        return CreateUnusedFodderGenesQuery()
            .Where(g => g.LastSeenAgent == agentName)
            .ToListAsync();
    }

    public Task<List<Gene>> GetUnusedVolumeGenes(string agentName)
    {
        return CreateUnusedVolumeGenesQuery()
            .Where(g => g.LastSeenAgent == agentName)
            .ToListAsync();
    }

    private IQueryable<Gene> CreateUnusedVolumeGenesQuery() =>
        dbContext.Genes.Where(x => !dbContext.VirtualDisks.Any(
            d => d.StorageIdentifier == "gene:" + x.GeneSet + ":" + x.Name && (d.AttachedDrives.Count == 0 || d.Children.Count == 0)));

    private IQueryable<Gene> CreateUnusedFodderGenesQuery() =>
        dbContext.Genes.Where(x => !dbContext.MetadataGenes.Any(mg => mg.GeneSet == x.GeneSet && mg.GeneName == x.Name));

    public Task<List<Gene>> FindUnusedGenes(string agentName) =>
        CreateUnusedGenesQuery()
            .Where(g => g.LastSeenAgent == agentName)
            .ToListAsync();

    public Task<bool> IsUnusedGene(Guid geneId) =>
        CreateUnusedGenesQuery()
            .Where(g => g.Id == geneId)
            .AnyAsync();

    private IQueryable<Gene> CreateUnusedGenesQuery() =>
        from gene in dbContext.Genes
        where gene.GeneType != GeneType.Volume || !dbContext.VirtualDisks.Any(
            d => d.Geneset == gene.GeneSet && d.Name == gene.Name && d.LastSeenAgent == gene.LastSeenAgent && (d.AttachedDrives.Count == 0 || d.Children.Count == 0))
        where gene.GeneType != GeneType.Fodder || !dbContext.MetadataGenes.Any(
            mg => mg.GeneSet == gene.GeneSet && mg.GeneName == gene.Name)
        select gene;

}
