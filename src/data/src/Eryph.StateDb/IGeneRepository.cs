using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

public interface IGeneRepository : IStateStoreRepository<Gene>
{
    Task<bool> IsUnusedVolumeGene(Guid geneId);

    Task<List<Gene>> GetUnusedVolumeGenes(string agentName);
}
