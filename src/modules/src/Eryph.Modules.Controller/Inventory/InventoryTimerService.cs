using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Rebus;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Inventory;

internal class InventoryTimerService : IHostedService, IDisposable
{
    private readonly IBus _bus;
    private Timer? _timer;
    private Task? _executingTask;

    public InventoryTimerService(IBus bus)
    {
       _bus = bus;
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
    private Task RunJobAsync()
    {
        return _bus.Advanced.Topics.Publish($"broadcast_{QueueNames.VMHostAgent}", new InventoryRequestedEvent());

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

