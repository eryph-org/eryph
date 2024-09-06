using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

public interface IGeneInventoryQueries
{
    Task<bool> IsUnusedVolumeGene(Guid geneId);

    Task<List<Gene>> GetUnusedVolumeGenes(string agentName);

    Task<bool> IsUnusedFodderGene(Guid geneId);
    
    Task<List<Gene>> GetUnusedFodderGenes(string agentName);
    
    Task<List<Gene>> FindUnusedGenes(string agentName);
    
    Task<bool> IsUnusedGene(Guid geneId);


    Task<GeneWithUsage?> GetGeneWithUsage(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<GeneWithUsage>> GetGenesWithUsage(CancellationToken cancellationToken = default);

    Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default);
}