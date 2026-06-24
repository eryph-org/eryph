using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

public interface IGeneInventoryQueries
{
    Task<List<Gene>> FindUnusedGenes(
        string agentName,
        CancellationToken cancellationToken = default);

    Task<bool> IsUnusedGene(
        Guid geneId,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetCatletsUsingGene(
        string agentName,
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken = default);

    Task<List<Guid>> GetDisksUsingGene(
        string agentName,
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken = default);
}
