using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// On startup, registers this module as a live component with the controller and
/// then requests its configuration. Registration is the service-catalog/liveness
/// signal; the config request is the explicit pull, announcing the versions the
/// component already holds so the controller returns only what is missing.
/// </summary>
internal sealed class ComponentRegistrationStartupHandler(
    IBus bus,
    ComponentIdentity identity,
    IComponentConfigState state,
    ILogger<ComponentRegistrationStartupHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Registering component {ComponentType} ({ComponentId}) on inbound queue {Queue}.",
            identity.ComponentType, identity.ComponentId, identity.InboundQueue);

        await bus.Advanced.Routing.Send(QueueNames.Controllers, new RegisterComponentCommand
        {
            ComponentId = identity.ComponentId,
            ComponentType = identity.ComponentType,
            InstanceId = identity.InstanceId,
            MachineName = identity.MachineName,
            InboundQueue = identity.InboundQueue,
            KnownConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
            AdvertisedEndpoints = identity.AdvertisedEndpoints.ToDictionary(kv => kv.Key, kv => kv.Value),
        });

        // Pull the current configuration from the controller.
        await bus.Advanced.Routing.Send(QueueNames.Controllers, new RequestConfigCommand
        {
            ComponentId = identity.ComponentId,
            ComponentType = identity.ComponentType,
            InboundQueue = identity.InboundQueue,
            KnownConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
        });
    }
}
