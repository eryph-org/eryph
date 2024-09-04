using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
internal class InventorizeGenePoolCommandHandler(
    IBus bus,
    ILogger logger,
    IGenePoolFactory genepoolFactory,
    IGenePoolInventoryFactory genePoolInventoryFactory,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    WorkflowOptions workflowOptions)
    : IHandleMessages<InventorizeGenePoolCommand>
{
    public Task Handle(InventorizeGenePoolCommand message) =>
        InventorizeGenePool().MatchAsync(
            RightAsync: c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
            LeftAsync: error =>
            {
                logger.LogError(error, "Inventory of gene pool on host {HostName} failed", Environment.MachineName);
                return Task.CompletedTask;
            });

    private EitherAsync<Error, UpdateGenePoolInventoryCommand> InventorizeGenePool() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let timestamp = DateTimeOffset.UtcNow
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePool = genepoolFactory.CreateLocal()
        let genePoolInventory = genePoolInventoryFactory.Create(genePoolPath, genePool)
        from inventoryData in genePoolInventory.InventorizeGenePool()
        select new UpdateGenePoolInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = inventoryData.ToList(),
        };
}
