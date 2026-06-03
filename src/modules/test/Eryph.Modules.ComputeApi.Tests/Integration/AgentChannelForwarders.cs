using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.Channels;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

/// <summary>
/// No-op forwarder used to satisfy the compute API's <see cref="IAgentChannelForwarder"/> dependency in
/// tests that never reach the data plane.
/// </summary>
public sealed class NullAgentChannelForwarder : IAgentChannelForwarder
{
    public Task ForwardAsync(
        WebSocket operatorSocket, string agentName, string token, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>
/// Records the arguments of the single forward call and closes the accepted operator socket so the
/// WebSocket handshake completes for the client.
/// </summary>
public sealed class CapturingAgentChannelForwarder : IAgentChannelForwarder
{
    public string? AgentName { get; private set; }
    public string? Token { get; private set; }
    public int CallCount { get; private set; }

    public async Task ForwardAsync(
        WebSocket operatorSocket, string agentName, string token, CancellationToken cancellationToken = default)
    {
        CallCount++;
        AgentName = agentName;
        Token = token;

        if (operatorSocket.State == WebSocketState.Open)
            await operatorSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }
}
