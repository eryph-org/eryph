using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    interface IZeroStateQueue
    {
        Task<ZeroStateQueueItem> DequeueAsync(CancellationToken cancellationToken = default);

        Task EnqueueAsync(ZeroStateQueueItem item, CancellationToken cancellationToken = default);
    }

    internal class ZeroStateQueue : IZeroStateQueue
    {
        public Task<ZeroStateQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task EnqueueAsync(ZeroStateQueueItem item, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
