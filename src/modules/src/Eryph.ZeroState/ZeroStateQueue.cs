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
        Task<ZeroStateQueueItem2<TChange>> DequeueAsync(CancellationToken cancellationToken = default);

        Task EnqueueAsync(ZeroStateQueueItem2<TChange> item, CancellationToken cancellationToken = default);
    }

    public class ZeroStateQueue<TChange> : IZeroStateQueue<TChange>
    {
        private readonly Channel<ZeroStateQueueItem2<TChange>> _channel =
            Channel.CreateUnbounded<ZeroStateQueueItem2<TChange>>();

        public async Task<ZeroStateQueueItem2<TChange>> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        public async Task EnqueueAsync(ZeroStateQueueItem2<TChange> item, CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(item, cancellationToken);
        }
    }
}
