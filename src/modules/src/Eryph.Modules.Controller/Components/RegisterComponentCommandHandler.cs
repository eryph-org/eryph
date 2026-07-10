using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Persists a component's registration (the service catalog + liveness). The
/// component requests its configuration separately via <see cref="RequestConfigCommand"/>.
/// </summary>
[UsedImplicitly]
internal sealed class RegisterComponentCommandHandler(
    IBus bus,
    IComponentRegistryService registry,
    ILogger<RegisterComponentCommandHandler> logger)
    : IHandleMessages<RegisterComponentCommand>
{
    public async Task Handle(RegisterComponentCommand message)
    {
        await registry.UpsertAsync(message, CancellationToken.None);
        logger.LogInformation(
            "Registered component {ComponentType} ({ComponentId}) on queue {Queue}.",
            message.ComponentType, message.ComponentId, message.InboundQueue);

        // A component that hosts endpoints contributes them to the Endpoints domain.
        // Re-evaluate and push so already-registered components pick up the new
        // advertiser; the refresh is a no-op when the aggregated content is unchanged.
        if (message.AdvertisedEndpoints is { Count: > 0 })
            await bus.Advanced.Routing.Send(
                QueueNames.Controllers,
                new RefreshConfigDomainCommand { Domain = ConfigDomain.Endpoints });

        // A host agent joining (or re-registering with a changed chassis priority) alters the OVN
        // gateway chassis topology; re-evaluate and push OvnCluster so the network component updates its
        // chassis groups without waiting for the next network sync. No-op when the topology is unchanged.
        if (message.ComponentType == ComponentType.VMHostAgent)
            await bus.Advanced.Routing.Send(
                QueueNames.Controllers,
                new RefreshConfigDomainCommand { Domain = ConfigDomain.OvnCluster });
    }
}
