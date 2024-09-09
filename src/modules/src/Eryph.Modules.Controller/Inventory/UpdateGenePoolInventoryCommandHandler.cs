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

        var outdatedGenes = await _geneRepository.ListAsync(
            new GeneSpecs.GetOutdated(message.AgentName, message.Timestamp));
        await _geneRepository.DeleteRangeAsync(outdatedGenes);
    }
}
