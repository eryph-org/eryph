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
/// Registers this module as a live component with the controller on startup,
/// announcing the configuration versions it already holds so the controller can
/// send only what is missing.
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
        });
    }
}
