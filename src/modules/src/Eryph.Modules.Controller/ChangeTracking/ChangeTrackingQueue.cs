using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.ChangeTracking;

internal interface IChangeTrackingQueue<TChange>
{
    int GetCount();

    Task<ChangeTrackingQueueItem<TChange>> DequeueAsync(
        CancellationToken cancellationToken = default);

    Task EnqueueAsync(
        ChangeTrackingQueueItem<TChange> item,
        CancellationToken cancellationToken = default);
}

internal class ChangeTrackingQueue<TChange> : IChangeTrackingQueue<TChange>
{
    private readonly Channel<ChangeTrackingQueueItem<TChange>> _channel =
        Channel.CreateBounded<ChangeTrackingQueueItem<TChange>>(
            new BoundedChannelOptions(5)
            {
                FullMode = BoundedChannelFullMode.Wait,
            });

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
        await _channel.Writer.WriteAsync(item, cancellationToken);
    }
}

internal class ChangeTrackingQueueItem<TChange>
{
    public ChangeTrackingQueueItem(Guid transactionId, TChange changes)
    {
        TransactionId = transactionId;
        Changes = changes;
    }

    public Guid TransactionId { get; }

    public TChange Changes { get; }
}