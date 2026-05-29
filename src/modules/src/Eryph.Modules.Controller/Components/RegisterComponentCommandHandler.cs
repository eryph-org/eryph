using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Persists a component's registration and replies with the configuration
/// snapshot it is entitled to and does not yet hold, routed to its inbound queue.
/// </summary>
[UsedImplicitly]
internal sealed class RegisterComponentCommandHandler(
    IBus bus,
    IComponentRegistryService registry,
    ConfigDistributionService distribution,
    ILogger<RegisterComponentCommandHandler> logger)
    : IHandleMessages<RegisterComponentCommand>
{
    public async Task Handle(RegisterComponentCommand message)
    {
        await registry.UpsertAsync(message, CancellationToken.None);
        logger.LogInformation(
            "Registered component {ComponentType} ({ComponentId}) on queue {Queue}.",
            message.ComponentType, message.ComponentId, message.InboundQueue);

        var bundles = await distribution.BuildSnapshotAsync(
            message.ComponentType, message.KnownConfigVersions, CancellationToken.None);

        await bus.Advanced.Routing.Send(message.InboundQueue, new ConfigSnapshotCommand
        {
            ComponentId = message.ComponentId,
            Bundles = bundles,
        });
    }
}
