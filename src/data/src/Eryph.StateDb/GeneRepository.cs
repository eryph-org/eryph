using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb;

public class GeneRepository(StateStoreContext dbContext)
    : StateStoreRepository<Gene>(dbContext),
        IGeneRepository
{
    private readonly StateStoreContext _dbContext = dbContext;

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
        _dbContext.Genes.Where(x => !_dbContext.VirtualDisks.Any(
            d => d.StorageIdentifier == "gene:" + x.GeneSet + ":" + x.Name && (d.AttachedDrives.Count == 0 || d.Children.Count == 0)));

    private IQueryable<Gene> CreateUnusedFodderGenesQuery() =>
        _dbContext.Genes.Where(x => !_dbContext.MetadataGenes.Any(mg => mg.GeneSet == x.GeneSet && mg.GeneName == x.Name));
}
