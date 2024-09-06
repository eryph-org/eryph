using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

public interface IGeneInventoryQueries
{
    Task<List<Gene>> FindUnusedGenes(string agentName);
    
    Task<bool> IsUnusedGene(Guid geneId);

    Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        GeneIdentifier geneId,
        CancellationToken cancellationToken = default);
}
