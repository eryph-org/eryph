using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Removes a component's registration when it deregisters on graceful shutdown, so it leaves the
/// service catalog immediately rather than being aged out by the heartbeat timeout.
/// </summary>
[UsedImplicitly]
internal sealed class DeregisterComponentCommandHandler(
    IComponentRegistryService registry,
    ILogger<DeregisterComponentCommandHandler> logger)
    : IHandleMessages<DeregisterComponentCommand>
{
    public async Task Handle(DeregisterComponentCommand message)
    {
        var removed = await registry.DeregisterAsync(
            message.ComponentId, message.InstanceId, CancellationToken.None);
        if (removed)
            logger.LogInformation(
                "Deregistered component {ComponentId} on graceful shutdown.", message.ComponentId);
    }
}
