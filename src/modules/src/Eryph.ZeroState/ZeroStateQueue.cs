using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    public interface IZeroStateQueue<TChange>
    {
        Task<ZeroStateQueueItem<TChange>> DequeueAsync(
            CancellationToken cancellationToken = default);

        Task EnqueueAsync(
            ZeroStateQueueItem<TChange> item,
            CancellationToken cancellationToken = default);
    }

    public class ZeroStateQueue<TChange> : IZeroStateQueue<TChange>
    {
        private readonly Channel<ZeroStateQueueItem<TChange>> _channel =
            Channel.CreateUnbounded<ZeroStateQueueItem<TChange>>();

        public async Task<ZeroStateQueueItem<TChange>> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        public async Task EnqueueAsync(ZeroStateQueueItem<TChange> item, CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(item, cancellationToken);
        }
    }

    public class ZeroStateQueueItem<TChange>
    {
        public Guid TransactionId { get; set; }

        public TChange Changes { get; init; }
    }
}
