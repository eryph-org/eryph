using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Rebus;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryTimerService : IHostedService, IDisposable
{
    private readonly IBus _bus;
    private readonly IStorageManagementAgentLocator _agentLocator;
    private Timer? _timer;
    private Task? _executingTask;

    public InventoryTimerService(
        IBus bus,
        IStorageManagementAgentLocator agentLocator)
    {
        _bus = bus;
        _agentLocator = agentLocator;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RunJobAsync();
        _timer = new Timer(ExecuteTask, null, TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(-1));

    }

    private void ExecuteTask(object? state)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _executingTask = ExecuteTaskAsync();
    }

    private async Task ExecuteTaskAsync()
    {
        await RunJobAsync();
        _timer?.Change(TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(-1));
    }

    /// <summary>
    /// This method is called when the <see cref="IHostedService"/> starts. The implementation should return a task 
    /// </summary>
    /// <returns>A <see cref="Task"/> that represents the long running operations.</returns>
    private async Task RunJobAsync()
    {
        // In the future, the gene pool might be shared between multiple agents. Hence,
        // the controller will need to pick a responsible agent.
        var agentName = _agentLocator.FindAgentForGenePool();
        await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{agentName}",
            new InventorizeGenePoolCommand()
            {
                AgentName = agentName,
            });
        await  _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        // Stop called without start
        if (_executingTask == null)
        {
            return;
        }

        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));


    }

    public void Dispose()
    {
        _timer?.Dispose();
    }


}

