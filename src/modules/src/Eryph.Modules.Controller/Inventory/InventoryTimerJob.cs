using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Rebus;
using Quartz;
using Rebus.Bus;
using SimpleInjector;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryTimerJob(Container container) : IJob
{
    private readonly IBus _bus = container.GetInstance<IBus>();
    private readonly IStorageManagementAgentLocator _agentLocator
        = container.GetInstance<IStorageManagementAgentLocator>();

    public async Task Execute(IJobExecutionContext context)
    {
        // In the future, the gene pool might be shared between multiple agents. Hence,
        // the controller will need to pick a responsible agent.
        var agentName = _agentLocator.FindAgentForGenePool();
        await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{agentName}",
        new InventorizeGenePoolCommand()
        {
            AgentName = agentName,
        });

        // Broadcast the inventory request (for virtual machines and disks) to all agents
        await _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());
    }
}
