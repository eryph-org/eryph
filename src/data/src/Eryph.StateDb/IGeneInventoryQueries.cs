using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}