using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Rebus;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Permanently decommissions a component: deletes its broker user (revoking bus access immediately,
/// independent of certificate expiry) and removes its registration from the catalog. The broker user
/// is removed even if no registration is found, so a component that already aged out can still be
/// revoked. Broker provisioners are resolved as a collection: empty when there is no managed broker
/// (eryph-zero), so this then only removes the registration.
/// </summary>
[UsedImplicitly]
internal sealed class DecommissionComponentCommandHandler(
    IBus bus,
    IComponentRegistryService registry,
    IEnumerable<IComponentBrokerProvisioner> brokerProvisioners,
    ILogger<DecommissionComponentCommandHandler> logger)
    : IHandleMessages<DecommissionComponentCommand>
{
    public async Task Handle(DecommissionComponentCommand message)
    {
        // Delete the broker user first: that is the actual revocation (the hard cutoff). Do it before
        // removing the registration so a failure here surfaces (the message retries) rather than
        // leaving a still-connectable component with no catalog row.
        foreach (var provisioner in brokerProvisioners)
            await provisioner.RemoveComponentAsync(message.ComponentId, CancellationToken.None);

        var removed = await registry.RemoveRegistrationAsync(message.ComponentId, CancellationToken.None);
        logger.LogInformation(
            "Decommissioned component {ComponentId} (broker user removed; registration removed: {Removed}).",
            message.ComponentId, removed);

        if (!removed)
            return;

        // Permanent removal changes the OVN gateway chassis topology (a decommissioned host agent can no
        // longer act as a gateway). Refresh OvnCluster so the network component drops its chassis. The
        // command does not carry the component type, so refresh on any removal; the refresh re-evaluates
        // the chassis and only pushes when it actually changed. Chassis are deliberately NOT refreshed on
        // graceful deregister, which is transient (a restarting agent) and would otherwise flap the group.
        await bus.Advanced.Routing.Send(
            QueueNames.Controllers,
            new RefreshConfigDomainCommand { Domain = ConfigDomain.OvnCluster });
    }
}
