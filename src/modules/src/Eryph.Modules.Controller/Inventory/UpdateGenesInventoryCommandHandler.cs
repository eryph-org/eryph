using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.StateDb;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

internal class UpdateGenesInventoryCommandHandler(
    IGeneRepository geneRepository)
    : UpdateGenePoolInventoryCommandHandlerBase(geneRepository),
        IHandleMessages<UpdateGenesInventoryCommand>
{
    public async Task Handle(UpdateGenesInventoryCommand message)
    {
        await AddOrUpdateGenes(message.AgentName, message.Timestamp, message.Inventory);
    }
}
