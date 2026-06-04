using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.Channels;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Eryph.Modules.HostAgent.Channels;

/// <summary>
/// Registers this agent's <see cref="IChannelService"/> as the channel recipient with the host-owned
/// in-process forwarder. Added by the host only where the compute API shares the process (eryph-zero);
/// in the split runtime the agent is reached over the network listener instead, so no recipient is
/// registered.
/// </summary>
[UsedImplicitly]
public sealed class ChannelRecipientRegistrationService(
    IAgentChannelRecipientRegistry registry,
    IChannelService channelService)
    : IHostedService
{
    private IDisposable? _registration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (channelService is not IAgentChannelRecipient recipient)
            throw new InvalidOperationException(
                $"The registered {nameof(IChannelService)} ({channelService.GetType().Name}) must also "
                + $"implement {nameof(IAgentChannelRecipient)} to be reached by the in-process forwarder.");

        _registration = registry.RegisterRecipient(recipient);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose();
        _registration = null;
        return Task.CompletedTask;
    }
}
