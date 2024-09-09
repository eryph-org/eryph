using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.GenePool;
using Eryph.Messages.Genes.Commands;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class UpdateGenePoolInventoryCommandHandler(
    IStateStoreRepository<Gene> geneRepository)
    : UpdateGenePoolInventoryCommandHandlerBase(geneRepository),
        IHandleMessages<UpdateGenePoolInventoryCommand>
{
    private readonly IStateStoreRepository<Gene> _geneRepository = geneRepository;

    public async Task Handle(UpdateGenePoolInventoryCommand message)
    {
        await AddOrUpdateGenes(message.AgentName, message.Timestamp, message.Inventory);

        // We cannot find the missing genes using the timestamp as the
        // updated genes have been persisted to the database. If we want
        // to optimize this in the future, we need to support transaction
        // such that we can call SaveChangesAsync() here.
        var geneIds = message.Inventory.Map(gene => gene.Id).ToList();
        var missingGenes = await _geneRepository.ListAsync(
            new GeneSpecs.GetMissing(message.AgentName, geneIds));
        await _geneRepository.DeleteRangeAsync(missingGenes);
    }
}
