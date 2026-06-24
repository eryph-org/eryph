using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.ChangeTracking;

public interface IChangeTrackingQueue<TChange>
{
    void Enable();

    int GetCount();

    Task<ChangeTrackingQueueItem<TChange>> DequeueAsync(
        CancellationToken cancellationToken = default);

    Task EnqueueAsync(
        ChangeTrackingQueueItem<TChange> item,
        CancellationToken cancellationToken = default);
}

public class ChangeTrackingQueue<TChange> : IChangeTrackingQueue<TChange>
{
    private readonly Channel<ChangeTrackingQueueItem<TChange>> _channel =
        Channel.CreateBounded<ChangeTrackingQueueItem<TChange>>(
            new BoundedChannelOptions(5)
            {
                FullMode = BoundedChannelFullMode.Wait,
            });

    private bool _enabled;

    public void Enable() => _enabled = true;

    public int GetCount() => _channel.Reader.Count;

    public async Task<ChangeTrackingQueueItem<TChange>> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    public async Task EnqueueAsync(
        ChangeTrackingQueueItem<TChange> item,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled)
            return;

        await _channel.Writer.WriteAsync(item, cancellationToken);
    }
}

public record ChangeTrackingQueueItem<TChange>(Guid TransactionId, TChange Changes);
