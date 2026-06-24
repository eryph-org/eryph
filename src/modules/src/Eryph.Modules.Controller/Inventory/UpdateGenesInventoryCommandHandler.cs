using System;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

internal class UpdateGenesInventoryCommandHandler(
    IStateStoreRepository<Gene> geneRepository)
    : UpdateGenePoolInventoryCommandHandlerBase(geneRepository),
        IHandleMessages<UpdateGenesInventoryCommand>
{
    public async Task Handle(UpdateGenesInventoryCommand message)
    {
        var agentName = message.AgentName ?? throw new InvalidOperationException("Agent name is required");
        var genes = message.Inventory ?? throw new InvalidOperationException("Inventory is required");
        await AddOrUpdateGenes(agentName, message.Timestamp, genes);
    }
}
