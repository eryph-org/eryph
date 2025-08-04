using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using Quartz;
using Rebus.Bus;
using SimpleInjector;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryTimerJob(Container container) : IJob
{
    public static readonly JobKey Key = new(nameof(InventoryTimerJob));

    private readonly IBus _bus = container.GetInstance<IBus>();
    private readonly ILogger _logger = container.GetInstance<ILogger<InventoryTimerJob>>();
    private readonly IStorageManagementAgentLocator _agentLocator
        = container.GetInstance<IStorageManagementAgentLocator>();

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Requesting scheduled inventory of all agents...");
        try
        {
            // In the future, the gene pool might be shared between multiple agents. Hence,
            // the controller will need to pick a responsible agent.
            var agentName = _agentLocator.FindAgentForGenePool();
            await _bus.Advanced.Routing.Send($"{QueueNames.GenePool}.{agentName}",
                new InventorizeGenePoolCommand()
                {
                    AgentName = agentName,
                });

            // Broadcast the inventory request (for virtual machines and disks) to all agents
            await _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request scheduled inventory of all agents");
        }
    }
}
