using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// Forwards an operator's accepted channel <see cref="WebSocket"/> to the host agent running a catlet,
/// bridging it to the guest hvsocket until either side closes. The compute API data-plane endpoint
/// depends only on this seam; the host supplies the implementation (mTLS network dial, or in-process).
/// </summary>
public interface IAgentChannelForwarder
{
    /// <summary>
    /// Forwards <paramref name="operatorSocket"/> to the agent <paramref name="agentName"/> with the
    /// one-time <paramref name="token"/> and bridges until either side closes. The implementation owns
    /// error handling and closes <paramref name="operatorSocket"/> on failure.
    /// </summary>
    Task ForwardAsync(
        WebSocket operatorSocket,
        string agentName,
        string token,
        CancellationToken cancellationToken);
}
