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
/// Removes a component's registration when it deregisters on graceful shutdown, so it leaves the
/// service catalog immediately rather than being aged out by the heartbeat timeout.
/// </summary>
[UsedImplicitly]
internal sealed class DeregisterComponentCommandHandler(
    IBus bus,
    IComponentRegistryService registry,
    ILogger<DeregisterComponentCommandHandler> logger)
    : IHandleMessages<DeregisterComponentCommand>
{
    public async Task Handle(DeregisterComponentCommand message)
    {
        var removed = await registry.DeregisterAsync(
            message.ComponentId, message.InstanceId, CancellationToken.None);
        if (!removed)
            return;

        logger.LogInformation(
            "Deregistered component {ComponentId} on graceful shutdown.", message.ComponentId);

        // A departing host agent changes the OVN gateway chassis topology. The deregister command does
        // not carry the component type, so refresh OvnCluster on any removal; the refresh re-evaluates
        // the chassis from the registry and only pushes to the network component when it actually
        // changed, so this is a no-op for non-host-agent components.
        await bus.Advanced.Routing.Send(
            QueueNames.Controllers,
            new RefreshConfigDomainCommand { Domain = ConfigDomain.OvnCluster });
    }
}
