using System;

namespace Eryph.Modules.AspNetCore.Channels;

/// <summary>
/// Lets the agent side register the <see cref="IAgentChannelRecipient"/> that the host-owned in-process
/// forwarder routes channels to. The agent registers on startup (and unregisters on shutdown via the
/// returned token) so the forwarder and the recipient meet through this seam without either module
/// referencing the other.
/// </summary>
public interface IAgentChannelRecipientRegistry
{
    /// <summary>
    /// Registers <paramref name="recipient"/> as the channel recipient. Disposing the returned token
    /// unregisters it.
    /// </summary>
    IDisposable RegisterRecipient(IAgentChannelRecipient recipient);
}
