using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Eryph.ZeroState;

internal interface IZeroStateQueue<TChange>
{
    Task<ZeroStateQueueItem<TChange>> DequeueAsync(
        CancellationToken cancellationToken = default);

    Task EnqueueAsync(
        ZeroStateQueueItem<TChange> item,
        CancellationToken cancellationToken = default);
}

internal class ZeroStateQueue<TChange> : IZeroStateQueue<TChange>
{
    private readonly Channel<ZeroStateQueueItem<TChange>> _channel =
        Channel.CreateBounded<ZeroStateQueueItem<TChange>>(
            new BoundedChannelOptions(5)
            {
                FullMode = BoundedChannelFullMode.Wait,
            });

    public async Task<ZeroStateQueueItem<TChange>> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    public async Task EnqueueAsync(
        ZeroStateQueueItem<TChange> item,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
    }
}

internal class ZeroStateQueueItem<TChange>
{
    public ZeroStateQueueItem(Guid transactionId, TChange changes)
    {
        TransactionId = transactionId;
        Changes = changes;
    }

    public Guid TransactionId { get; }

    public TChange Changes { get; }
}