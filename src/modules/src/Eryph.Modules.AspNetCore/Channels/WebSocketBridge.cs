using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// Reverse-proxies bytes bidirectionally between two <see cref="WebSocket"/> instances until either
/// side closes (or the supplied token is cancelled). The data is opaque — for the EGS remote channel
/// it is raw, end-to-end SSH ciphertext, so the bridge never inspects or reframes it; it only forwards
/// the message/close frames each side emits.
/// </summary>
/// <remarks>
/// Shared by the compute API data-plane endpoint (operator WebSocket ⇄ agent <see cref="ClientWebSocket"/>)
/// and, later, by the host agent's channel listener (agent WebSocket ⇄ guest hvsocket WebSocket). Buffers
/// are rented from <see cref="ArrayPool{T}"/> so a high connection count does not churn the GC.
/// </remarks>
public static class WebSocketBridge
{
    // SSH carries small interactive packets; 16 KiB is comfortably larger than a typical SSH record and
    // keeps the rented buffers cheap. Larger transfers are split across multiple receive iterations.
    private const int BufferSize = 16 * 1024;

    /// <summary>
    /// Pumps both directions until either socket reports a close (or <paramref name="cancellationToken"/>
    /// fires), then closes both sockets cleanly. Returns when both pump directions have finished.
    /// </summary>
    public static async Task PumpAsync(
        WebSocket first,
        WebSocket second,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        // A close (or fault) on one direction must tear down the other so a half-open socket does not leave
        // a pump blocked forever in ReceiveAsync. The linked token is the shared stop signal for both.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var firstToSecond = PumpOneDirectionAsync(first, second, linkedCts);
        var secondToFirst = PumpOneDirectionAsync(second, first, linkedCts);

        await Task.WhenAll(firstToSecond, secondToFirst).ConfigureAwait(false);
    }

    /// <summary>
    /// Bridges a <see cref="WebSocket"/> to a raw duplex byte <see cref="Stream"/> until either side
    /// closes (or <paramref name="cancellationToken"/> fires). This is the host-agent side of the EGS
    /// remote channel: the operator's WebSocket is bridged to the guest hvsocket, which is a raw
    /// <see cref="Stream"/> (a <see cref="System.Net.Sockets.NetworkStream"/> over the connected
    /// hvsocket), not a WebSocket. The bytes are opaque end-to-end SSH ciphertext and are forwarded
    /// without inspection or reframing.
    /// </summary>
    public static async Task PumpAsync(
        WebSocket webSocket,
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        ArgumentNullException.ThrowIfNull(stream);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var webSocketToStream = PumpWebSocketToStreamAsync(webSocket, stream, linkedCts);
        var streamToWebSocket = PumpStreamToWebSocketAsync(stream, webSocket, linkedCts);

        await Task.WhenAll(webSocketToStream, streamToWebSocket).ConfigureAwait(false);
    }

    private static async Task PumpWebSocketToStreamAsync(
        WebSocket source,
        Stream destination,
        CancellationTokenSource linkedCts)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var token = linkedCts.Token;
            while (!token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await source.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                try
                {
                    await destination.WriteAsync(
                        buffer.AsMemory(0, result.Count), token).ConfigureAwait(false);
                    await destination.FlushAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // hvsocket dropped (catlet stop / migration). Tear down the other direction.
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await linkedCts.CancelAsync().ConfigureAwait(false);
        }
    }

    private static async Task PumpStreamToWebSocketAsync(
        Stream source,
        WebSocket destination,
        CancellationTokenSource linkedCts)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var token = linkedCts.Token;
            while (!token.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                if (read == 0)
                {
                    // The hvsocket reached end-of-stream; signal a clean close to the WebSocket peer.
                    if (destination.State == WebSocketState.Open)
                    {
                        try
                        {
                            await destination.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        catch (WebSocketException)
                        {
                            // Destination already gone; ignore.
                        }
                    }

                    break;
                }

                if (destination.State != WebSocketState.Open)
                    break;

                try
                {
                    await destination.SendAsync(
                        new ArraySegment<byte>(buffer, 0, read),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await linkedCts.CancelAsync().ConfigureAwait(false);
        }
    }

    private static async Task PumpOneDirectionAsync(
        WebSocket source,
        WebSocket destination,
        CancellationTokenSource linkedCts)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var token = linkedCts.Token;
            while (!token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await source.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The other direction (or the caller) asked us to stop.
                    break;
                }
                catch (WebSocketException)
                {
                    // The peer dropped the connection abruptly (catlet stop, migration, network loss).
                    // Nothing to orchestrate — tear down the other direction and exit.
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Mirror the close back to the destination so the far side sees a clean shutdown.
                    if (destination.State == WebSocketState.Open)
                    {
                        try
                        {
                            await destination.CloseOutputAsync(
                                result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                                result.CloseStatusDescription,
                                CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (WebSocketException)
                        {
                            // Destination already gone; ignore.
                        }
                    }

                    break;
                }

                if (destination.State != WebSocketState.Open)
                    break;

                try
                {
                    await destination.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            // Signal the other direction to stop; its ReceiveAsync is observing the same linked token.
            await linkedCts.CancelAsync().ConfigureAwait(false);
        }
    }
}
