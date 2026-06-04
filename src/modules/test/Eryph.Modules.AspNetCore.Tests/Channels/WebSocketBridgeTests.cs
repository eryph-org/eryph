using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.Channels;
using FluentAssertions;

namespace Eryph.Modules.AspNetCore.Tests.Channels;

public class WebSocketBridgeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task WebSocketToStream_ForwardsBytes_ThenTearsDownOnClose()
    {
        var webSocket = new ScriptedWebSocket();
        var stream = new GatedDuplexStream();

        // The web socket delivers a frame then closes; the stream side blocks (no read queued) until
        // the close cancels it. The close on one direction must tear down the other.
        webSocket.QueueReceive("ping"u8.ToArray());
        webSocket.QueueClose();

        await WebSocketBridge.PumpAsync(webSocket, stream, CancellationToken.None)
            .WaitAsync(Timeout);

        Encoding.UTF8.GetString(stream.Written).Should().Be("ping");
    }

    [Fact]
    public async Task StreamToWebSocket_ForwardsBytes_AndSignalsCleanCloseOnEndOfStream()
    {
        var webSocket = new ScriptedWebSocket();
        var stream = new GatedDuplexStream();

        stream.QueueRead("pong"u8.ToArray());
        stream.QueueEndOfStream();

        await WebSocketBridge.PumpAsync(webSocket, stream, CancellationToken.None)
            .WaitAsync(Timeout);

        Encoding.UTF8.GetString(webSocket.SentBytes()).Should().Be("pong");
        webSocket.CloseOutputStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public async Task WebSocketToWebSocket_ForwardsBytes_AndMirrorsClose()
    {
        var first = new ScriptedWebSocket();
        var second = new ScriptedWebSocket();

        first.QueueReceive("from-first"u8.ToArray());
        first.QueueClose();

        await WebSocketBridge.PumpAsync(first, second, CancellationToken.None)
            .WaitAsync(Timeout);

        Encoding.UTF8.GetString(second.SentBytes()).Should().Be("from-first");
        second.CloseOutputStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public async Task PumpAsync_CompletesWhenCancelledUpFront()
    {
        var webSocket = new ScriptedWebSocket();
        var stream = new GatedDuplexStream();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await WebSocketBridge.PumpAsync(webSocket, stream, cts.Token).WaitAsync(Timeout);
    }

    private sealed class ScriptedWebSocket : WebSocket
    {
        private readonly Channel<ReceiveStep> _incoming = Channel.CreateUnbounded<ReceiveStep>();
        private readonly List<byte[]> _sent = new();
        private readonly object _gate = new();

        public void QueueReceive(byte[] data) => _incoming.Writer.TryWrite(ReceiveStep.ForData(data));

        public void QueueClose() => _incoming.Writer.TryWrite(ReceiveStep.ForClose());

        public byte[] SentBytes()
        {
            lock (_gate)
                return _sent.SelectMany(x => x).ToArray();
        }

        public WebSocketCloseStatus? CloseOutputStatus { get; private set; }

        public override WebSocketState State { get; } = WebSocketState.Open;

        public override async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var step = await _incoming.Reader.ReadAsync(cancellationToken);
            if (step.IsClose)
                return new WebSocketReceiveResult(
                    0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, null);

            Array.Copy(step.Data!, 0, buffer.Array!, buffer.Offset, step.Data!.Length);
            return new WebSocketReceiveResult(step.Data.Length, WebSocketMessageType.Binary, true);
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken)
        {
            lock (_gate)
                _sent.Add(buffer.ToArray());
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseOutputStatus = closeStatus;
            return Task.CompletedTask;
        }

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public override void Dispose() { }

        private readonly record struct ReceiveStep(bool IsClose, byte[]? Data)
        {
            public static ReceiveStep ForData(byte[] data) => new(false, data);
            public static ReceiveStep ForClose() => new(true, null);
        }
    }

    private sealed class GatedDuplexStream : Stream
    {
        private readonly Channel<byte[]?> _reads = Channel.CreateUnbounded<byte[]?>();
        private readonly MemoryStream _written = new();

        public void QueueRead(byte[] data) => _reads.Writer.TryWrite(data);

        // A null chunk signals end-of-stream (ReadAsync returns 0).
        public void QueueEndOfStream() => _reads.Writer.TryWrite(null);

        public byte[] Written
        {
            get
            {
                lock (_written)
                    return _written.ToArray();
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var chunk = await _reads.Reader.ReadAsync(cancellationToken);
            if (chunk is null)
                return 0;

            chunk.AsSpan().CopyTo(buffer.Span);
            return chunk.Length;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            lock (_written)
                _written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
