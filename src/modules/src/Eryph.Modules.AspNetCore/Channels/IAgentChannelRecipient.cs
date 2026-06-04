using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// The agent-side endpoint of the EGS remote channel. Given a one-time channel token, it validates and
/// consumes the token and opens the guest hvsocket, returning it as a raw duplex byte stream (or
/// <see langword="null"/> when the token is unknown, used, or expired). The in-process forwarder routes
/// to a registered recipient so it never depends on the agent module's implementation.
/// </summary>
public interface IAgentChannelRecipient
{
    Task<Stream?> OpenChannelAsync(string token, CancellationToken cancellationToken = default);
}
