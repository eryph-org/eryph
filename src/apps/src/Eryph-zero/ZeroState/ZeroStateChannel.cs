using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.ZeroState
{
    public interface IZeroStateChannel<T>
    {
        ValueTask<T> ReadAsync(CancellationToken cancellationToken = default);

        ValueTask WriteAsync(T value, CancellationToken cancellationToken = default);
    }

    internal class ZeroStateChannel<T> : IZeroStateChannel<T>
    {
        private readonly Channel<T> _channel;

        public ZeroStateChannel()
        {
            _channel = Channel.CreateUnbounded<T>();
        }

        public ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
        {
            return _channel.Reader.ReadAsync(cancellationToken);
        }

        public ValueTask WriteAsync(T value, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(value, cancellationToken);
        }
    }
}
