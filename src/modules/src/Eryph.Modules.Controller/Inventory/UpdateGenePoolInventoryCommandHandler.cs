using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Resources.GenePool;
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
        foreach (var geneSetData in message.Inventory)
        {
            await AddOrUpdateGeneSet(message.Timestamp, geneSetData);
        }

        // TODO Remove outdated genesets and genes
    }

    private async Task AddOrUpdateGeneSet(DateTimeOffset timestamp, GeneSetData geneSetData)
    {
        var dbGeneSet = await stateStore.For<GeneSet>().GetBySpecAsync(
            new GeneSetSpecs.GetForInventory(geneSetData.Id));
        if (dbGeneSet is null)
        {
            dbGeneSet = new GeneSet
            {
                Id = Guid.NewGuid(),
                Organization = geneSetData.Id.Organization.Value,
                Name = geneSetData.Id.GeneSet.Value,
                Tag = geneSetData.Id.Tag.Value,
                LastSeen = timestamp,
                Hash = "abc",
                Genes = [],
            };
            await stateStore.For<GeneSet>().AddAsync(dbGeneSet);
        }
        else
        {
            dbGeneSet.LastSeen = timestamp;
        }

        foreach (var geneData in geneSetData.Genes)
        {
            var dbGene = dbGeneSet.Genes.FirstOrDefault(
                g => g.Name == geneData.Id.GeneName.Value);
            if (dbGene is null)
            {
                dbGene = new Gene
                {
                    Id = Guid.NewGuid(),
                    GeneType = geneData.GeneType,
                    Name = geneData.Id.GeneName.Value,
                    Size = geneData.Size,
                    Hash = geneData.Hash,
                    LastSeen = timestamp
                };
                dbGeneSet.Genes.Add(dbGene);
            }
            else
            {
                dbGene.LastSeen = timestamp;
            }
        }
    }
}
