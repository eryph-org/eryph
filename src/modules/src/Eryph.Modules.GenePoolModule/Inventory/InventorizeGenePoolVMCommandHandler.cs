using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.GenePool.Genetics;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.GenePool.Inventory;

[UsedImplicitly]
internal class InventorizeGenePoolCommandHandler(
    IBus bus,
    ILogger logger,
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genepoolFactory,
    IGenePoolInventoryFactory genePoolInventoryFactory,
    WorkflowOptions workflowOptions)
    : IHandleMessages<InventorizeGenePoolCommand>
{
    public Task Handle(InventorizeGenePoolCommand message) =>
        InventorizeGenePool().MatchAsync(
            RightAsync: c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
            LeftAsync: error =>
            {
                logger.LogError(error, "Inventory of gene pool failed");
                return Task.CompletedTask;
            });

    private EitherAsync<Error, UpdateGenePoolInventoryCommand> InventorizeGenePool() =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let timestamp = DateTimeOffset.UtcNow
        let genePool = genepoolFactory.CreateLocal(genePoolPath)
        let genePoolInventory = genePoolInventoryFactory.Create(genePoolPath, genePool)
        from inventoryData in genePoolInventory.InventorizeGenePool()
            .RunWithCancel(CancellationToken.None)
            .ToEitherAsync()
        select new UpdateGenePoolInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = inventoryData.ToList(),
        };
}
