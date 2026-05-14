using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class ChangeTrackingBackgroundService<TChange> : BackgroundService
{
    private readonly Container _container;
    private readonly ILogger<ChangeTrackingBackgroundService<TChange>> _logger;
    private readonly IChangeTrackingQueue<TChange> _queue;

    public ChangeTrackingBackgroundService(
        Container container,
        ILogger<ChangeTrackingBackgroundService<TChange>> logger,
        IChangeTrackingQueue<TChange> queue)
    {
        _container = container;
        _logger = logger;
        _queue = queue;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Enable the queue synchronously during StartAsync so producers
        // can never observe a disabled queue. The Host awaits StartAsync
        // before returning, but does not wait for ExecuteAsync to actually
        // begin running, so enabling inside ExecuteAsync races with the
        // first SaveChanges on busy hosts (e.g. CI agents).
        _queue.Enable();
        _logger.LogWarning("CTDIAG BG StartAsync enabled queue for {TChange}", typeof(TChange).Name);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("CTDIAG BG ExecuteAsync running for {TChange}", typeof(TChange).Name);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queueItem = await _queue.DequeueAsync(stoppingToken);
                _logger.LogWarning("CTDIAG BG dequeued {TChange} tx={Tx}", typeof(TChange).Name, queueItem.TransactionId);
                await HandleChangeAsync(queueItem);
            }
            catch (OperationCanceledException)
            {
                // The exception will occur when the host is stopped.
                // We swallow it and flush the remaining queue.
            }
        }

        await FlushQueue();
    }

    private async Task FlushQueue()
    {
        _logger.LogWarning("CTDIAG BG flushing {Count} items for {TChange}", _queue.GetCount(), typeof(TChange).Name);
        while (_queue.GetCount() > 0)
        {
            var queueItem = await _queue.DequeueAsync();
            _logger.LogWarning("CTDIAG BG flush dequeued {TChange} tx={Tx}", typeof(TChange).Name, queueItem.TransactionId);
            await HandleChangeAsync(queueItem);
        }
    }

    private async Task HandleChangeAsync(ChangeTrackingQueueItem<TChange> queueItem)
    {
        _logger.LogDebug("Processing changes of transaction {TransactionId}", queueItem.TransactionId);

        try
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var handler = scope.GetInstance<IChangeHandler<TChange>>();
            await handler.HandleChangeAsync(queueItem.Changes);
            _logger.LogWarning("CTDIAG BG handler completed {TChange} tx={Tx}", typeof(TChange).Name, queueItem.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("CTDIAG BG handler FAILED {TChange}: {Ex}", typeof(TChange).Name, ex.GetType().Name + ": " + ex.Message);
            // We need to catch all exceptions here. Otherwise, the background
            // service will stop when an exception occurs. We always want to write
            // as many of the changes as possible to the filesystem.
            _logger.LogError(ex, "Failed to process changes of transaction {TransactionId}", queueItem.TransactionId);
        }
    }
}
