using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.GenePool;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Inventory;

internal abstract class UpdateGenePoolInventoryCommandHandlerBase(
    IStateStoreRepository<Gene> geneRepository)
{
    protected async Task AddOrUpdateGenes(
        string agentName,
        DateTimeOffset timestamp,
        IReadOnlyList<GeneData> genes)
    {
        foreach (var geneData in genes)
        {
            await AddOrUpdateGene(agentName, timestamp, geneData);
        }
    }

    private async Task AddOrUpdateGene(
        string agentName,
        DateTimeOffset timestamp,
        GeneData geneData)
    {
        var dbGene = await geneRepository.GetBySpecAsync(
            new GeneSpecs.GetForInventory(agentName, geneData.GeneType, geneData.Id));

        if (dbGene is null)
        {
            dbGene = new Gene
            {
                Id = Guid.NewGuid(),
                GeneType = geneData.GeneType,
                GeneId = geneData.Id.Value,
                Size = geneData.Size,
                Hash = geneData.Hash,
                LastSeen = timestamp,
                LastSeenAgent = agentName,
            };
            await geneRepository.AddAsync(dbGene);
        }
        else
        {
            dbGene.LastSeen = timestamp;
        }
    }
}
