using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.GenePool;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
public class UpdateGenePoolInventoryCommandHandler(
    IStateStore stateStore)
    : IHandleMessages<UpdateGenePoolInventoryCommand>
{
    public async Task Handle(UpdateGenePoolInventoryCommand message)
    {
        foreach (var geneData in message.Inventory)
        {
            await AddOrUpdateGene(message.AgentName, message.Timestamp, geneData);
        }

        // TODO Remove outdated genesets and genes
    }

    private async Task AddOrUpdateGene(
        string agentName,
        DateTimeOffset timestamp,
        GeneData geneData)
    {
        var dbGene = await stateStore.For<Gene>().GetBySpecAsync(
            new GeneSpecs.GetForInventory(agentName, geneData.GeneType, geneData.Id));

        if (dbGene is null)
        {
            dbGene = new Gene
            {
                Id = Guid.NewGuid(),
                GeneType = geneData.GeneType,
                GeneSet = geneData.Id.GeneSet.Value,
                Name = geneData.Id.GeneName.Value,
                Size = geneData.Size,
                Hash = geneData.Hash,
                LastSeen = timestamp,
                LastSeenAgent = agentName,
            };
            await stateStore.For<Gene>().AddAsync(dbGene);
        }
        else
        {
            dbGene.LastSeen = timestamp;
        }
    }
}
