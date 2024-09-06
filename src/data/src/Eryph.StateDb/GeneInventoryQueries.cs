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
    private readonly ISpecificationEvaluator _specificationEvaluator = new SpecificationEvaluator();

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

    public Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default) =>
        dbContext.Catlets
            .Where(c => c.AgentName == agentName
                        && dbContext.MetadataGenes
                            .Any(mg => mg.GeneSet == geneId.GeneSet.Value
                                       && mg.GeneName == geneId.GeneName.Value
                                       && c.MetadataId == mg.MetadataId))
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<GeneWithUsage?> GetGeneWithUsage(
        Guid id,
        CancellationToken cancellationToken = default) =>
        CreateGeneWithUsageQuery()
            .Where(g => g.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<GeneWithUsage>> GetGenesWithUsage(CancellationToken cancellationToken = default) =>
        CreateGeneWithUsageQuery()
            .ToListAsync(cancellationToken);

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

    public Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default) =>
        dbContext.VirtualDisks
            .Where(d => d.LastSeenAgent == agentName
                        && d.Geneset == geneId.GeneSet.Value
                        && d.GeneName == geneId.GeneName.Value)
            .SelectMany(d => d.Children)
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Gene> CreateUnusedGenesQuery() =>
        from gene in dbContext.Genes
        where gene.GeneType != GeneType.Volume || !dbContext.VirtualDisks.Any(
            d => d.Geneset == gene.GeneSet && d.Name == gene.Name && d.LastSeenAgent == gene.LastSeenAgent && (d.AttachedDrives.Count == 0 || d.Children.Count == 0))
        where gene.GeneType != GeneType.Fodder || !dbContext.MetadataGenes.Any(
            mg => mg.GeneSet == gene.GeneSet && mg.GeneName == gene.Name)
        select gene;

    private IQueryable<GeneWithUsage> CreateGeneWithUsageQuery() =>
        from gene in dbContext.Genes
        select new GeneWithUsage
        {
            Id = gene.Id,
            GeneType = gene.GeneType,
            GeneSet = gene.GeneSet,
            Name = gene.Name,
            Size = gene.Size,
            Hash = gene.Hash,
            Catlets = string.Join(";", dbContext.Catlets.Where(c => 
                dbContext.MetadataGenes.Any(mg => mg.GeneSet == gene.GeneSet && mg.GeneName == gene.Name && c.MetadataId == mg.MetadataId)
                && c.AgentName == gene.LastSeenAgent)
                .Select(c => c.Id)),
            Disks = string.Join(";", dbContext.VirtualDisks.Where(d =>
                d.LastSeenAgent == gene.LastSeenAgent
                && d.Geneset == gene.GeneSet
                && d.GeneName == gene.Name
                && d.ParentId != null)
                .Select(d => d.ParentId))
        };
}
