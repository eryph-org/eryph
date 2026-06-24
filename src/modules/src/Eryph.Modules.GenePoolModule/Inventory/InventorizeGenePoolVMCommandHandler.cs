using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.Messages.Genes.Commands;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Inventory;

[UsedImplicitly]
internal class InventorizeGenePoolCommandHandler(
    IBus bus,
    ILogger logger,
    IGenePoolInventory genePoolInventory,
    WorkflowOptions workflowOptions)
    : IHandleMessages<InventorizeGenePoolCommand>
{
    public async Task Handle(InventorizeGenePoolCommand message)
    {
        var result = await InventorizeGenePool().RunWithCancel(CancellationToken.None);
        await result.Match(
            async c => await bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
            error =>
            {
                logger.LogError(error, "Inventory of gene pool failed");
                return Task.CompletedTask;
            });
    }

    private Aff<CancelRt, UpdateGenePoolInventoryCommand> InventorizeGenePool() =>
        from _ in SuccessAff(unit)
        let timestamp = DateTimeOffset.UtcNow
        from inventoryData in genePoolInventory.InventorizeGenePool()
        select new UpdateGenePoolInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = inventoryData.ToList(),
        };
}
