using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ModuleCore.ChangeTracking;

public class ChangeTrackingBackgroundService<TChange>(
    Container container,
    ILogger<ChangeTrackingBackgroundService<TChange>> logger,
    IChangeTrackingQueue<TChange> queue)
    : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Enable the queue synchronously during StartAsync so producers
        // can never observe a disabled queue. The Host awaits StartAsync
        // before returning, but does not wait for ExecuteAsync to actually
        // begin running, so enabling inside ExecuteAsync races with the
        // first SaveChanges on busy hosts (e.g. CI agents).
        queue.Enable();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var queueItem = await queue.DequeueAsync(stoppingToken);
                await HandleChangeAsync(queueItem);
            }
            catch (OperationCanceledException)
            {
                // The exception will occur when the host is stopped.
                // We swallow it and flush the remaining queue.
            }

        await FlushQueue();
    }

    private async Task FlushQueue()
    {
        logger.LogInformation("Going to flush {Count} remaining queue item(s).",
            queue.GetCount());
        while (queue.GetCount() > 0)
        {
            var queueItem = await queue.DequeueAsync();
            await HandleChangeAsync(queueItem);
        }
    }

    private async Task HandleChangeAsync(ChangeTrackingQueueItem<TChange> queueItem)
    {
        logger.LogDebug("Processing changes of transaction {TransactionId}", queueItem.TransactionId);

        try
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
            var handler = scope.GetInstance<IChangeHandler<TChange>>();
            await handler.HandleChangeAsync(queueItem.Changes);
        }
        catch (Exception ex)
        {
            // We need to catch all exceptions here. Otherwise, the background
            // service will stop when an exception occurs. We always want to write
            // as many of the changes as possible to the filesystem.
            logger.LogError(ex, "Failed to process changes of transaction {TransactionId}", queueItem.TransactionId);
        }
    }
}
