using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// In-process implementation of the channel rendezvous used when the agent runs in the same process as
/// the compute API (eryph-zero). The host owns this single instance and wires it both ways: the compute
/// API consumes it as <see cref="IAgentChannelForwarder"/>, and the agent registers its
/// <see cref="IAgentChannelRecipient"/> through <see cref="IAgentChannelRecipientRegistry"/>. It bridges
/// the operator WebSocket to the recipient's guest stream without a network hop or TLS, and depends on no
/// module's implementation.
/// </summary>
public sealed class InProcessAgentChannelForwarder
    : IAgentChannelForwarder, IAgentChannelRecipientRegistry
{
    private volatile IAgentChannelRecipient? _recipient;

    public async Task ForwardAsync(
        WebSocket operatorSocket,
        string agentName,
        string token,
        CancellationToken cancellationToken)
    {
        // agentName is unused: there is a single in-process agent.
        var recipient = _recipient;
        if (recipient is null)
        {
            await CloseQuietly(operatorSocket).ConfigureAwait(false);
            return;
        }

        var guestStream = await recipient.OpenChannelAsync(token, cancellationToken).ConfigureAwait(false);
        if (guestStream is null)
        {
            await CloseQuietly(operatorSocket).ConfigureAwait(false);
            return;
        }

        await using (guestStream)
        {
            await WebSocketBridge.PumpAsync(operatorSocket, guestStream, cancellationToken).ConfigureAwait(false);
        }
    }

    public IDisposable RegisterRecipient(IAgentChannelRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _recipient = recipient;
        return new Registration(this, recipient);
    }

    private static async Task CloseQuietly(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation, null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // Peer already gone; nothing to do.
        }
    }

    private sealed class Registration(InProcessAgentChannelForwarder owner, IAgentChannelRecipient recipient)
        : IDisposable
    {
        public void Dispose()
        {
            // Only clear if a newer registration has not replaced this one.
            if (ReferenceEquals(owner._recipient, recipient))
                owner._recipient = null;
        }
    }
}
