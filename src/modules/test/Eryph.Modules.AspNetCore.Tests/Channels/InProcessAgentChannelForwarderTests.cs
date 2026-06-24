using System.Net.WebSockets;
using System.Threading.Channels;
using Eryph.Modules.AspNetCore.Channels;
using FluentAssertions;

namespace Eryph.Modules.AspNetCore.Tests.Channels;

public class InProcessAgentChannelForwarderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ForwardAsync_NoRecipientRegistered_ClosesOperatorSocket()
    {
        var forwarder = new InProcessAgentChannelForwarder();
        var operatorSocket = new RecordingWebSocket();

        await forwarder.ForwardAsync(operatorSocket, "agent", "token", CancellationToken.None)
            .WaitAsync(Timeout);

        operatorSocket.Closed.Should().BeTrue();
    }

    [Fact]
    public async Task ForwardAsync_RecipientReturnsNull_ClosesOperatorSocket()
    {
        var forwarder = new InProcessAgentChannelForwarder();
        var recipient = new StubRecipient(_ => null);
        forwarder.RegisterRecipient(recipient);
        var operatorSocket = new RecordingWebSocket();

        await forwarder.ForwardAsync(operatorSocket, "agent", "the-token", CancellationToken.None)
            .WaitAsync(Timeout);

        recipient.Calls.Should().Be(1);
        recipient.LastToken.Should().Be("the-token");
        operatorSocket.Closed.Should().BeTrue();
    }

    [Fact]
    public async Task ForwardAsync_RecipientReturnsStream_BridgesAndDisposesStream()
    {
        var forwarder = new InProcessAgentChannelForwarder();
        var guestStream = new TrackingStream();
        var recipient = new StubRecipient(_ => guestStream);
        forwarder.RegisterRecipient(recipient);

        var operatorSocket = new RecordingWebSocket();
        // The operator closes immediately; the bridge tears down and ForwardAsync returns.
        operatorSocket.QueueClose();

        await forwarder.ForwardAsync(operatorSocket, "agent", "the-token", CancellationToken.None)
            .WaitAsync(Timeout);

        recipient.Calls.Should().Be(1);
        guestStream.Disposed.Should().BeTrue("the forwarder owns and disposes the guest stream");
    }

    [Fact]
    public async Task ForwardAsync_AfterRecipientUnregistered_ClosesOperatorSocket()
    {
        var forwarder = new InProcessAgentChannelForwarder();
        var recipient = new StubRecipient(_ => new MemoryStream());
        var registration = forwarder.RegisterRecipient(recipient);

        registration.Dispose();

        var operatorSocket = new RecordingWebSocket();
        await forwarder.ForwardAsync(operatorSocket, "agent", "token", CancellationToken.None)
            .WaitAsync(Timeout);

        recipient.Calls.Should().Be(0);
        operatorSocket.Closed.Should().BeTrue();
    }

    private sealed class StubRecipient(Func<string, Stream?> open) : IAgentChannelRecipient
    {
        public int Calls { get; private set; }
        public string? LastToken { get; private set; }

        public Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = token;
            return Task.FromResult(open(token));
        }
    }

    private sealed class TrackingStream : Stream
    {
        private readonly Channel<byte[]?> _reads = Channel.CreateUnbounded<byte[]?>();
        public bool Disposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            // Block until the bridge is torn down (cancellation), mimicking an idle guest socket.
            var chunk = await _reads.Reader.ReadAsync(cancellationToken);
            return chunk?.Length ?? 0;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class RecordingWebSocket : WebSocket
    {
        private readonly Channel<bool> _incoming = Channel.CreateUnbounded<bool>();
        private WebSocketState _state = WebSocketState.Open;

        public bool Closed { get; private set; }

        public override WebSocketState State => _state;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public void QueueClose() => _incoming.Writer.TryWrite(true);

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await _incoming.Reader.ReadAsync(cancellationToken);
            return new WebSocketReceiveResult(
                0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            Closed = true;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Abort()
        {
        }

        public override void Dispose()
        {
        }
    }
}
