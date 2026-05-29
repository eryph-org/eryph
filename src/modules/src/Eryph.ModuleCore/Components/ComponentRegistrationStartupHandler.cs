using System.Collections.Generic;
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
/// announcing the configuration versions it already holds (so the controller can
/// send only what is missing) and the capabilities it advertises (e.g. datastores
/// and environments).
/// </summary>
internal sealed class ComponentRegistrationStartupHandler(
    IBus bus,
    ComponentIdentity identity,
    IComponentConfigState state,
    IEnumerable<IComponentCapabilitiesProvider> capabilitiesProviders,
    ILogger<ComponentRegistrationStartupHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Registering component {ComponentType} ({ComponentId}) on inbound queue {Queue}.",
            identity.ComponentType, identity.ComponentId, identity.InboundQueue);

        var capabilities = new Dictionary<string, string>();
        foreach (var provider in capabilitiesProviders)
        {
            var contributed = await provider.GetCapabilitiesAsync(cancellationToken);
            foreach (var (key, value) in contributed)
                capabilities[key] = value;
        }

        await bus.Advanced.Routing.Send(QueueNames.Controllers, new RegisterComponentCommand
        {
            ComponentId = identity.ComponentId,
            ComponentType = identity.ComponentType,
            InstanceId = identity.InstanceId,
            MachineName = identity.MachineName,
            InboundQueue = identity.InboundQueue,
            Capabilities = capabilities,
            KnownConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
        });
    }
}
