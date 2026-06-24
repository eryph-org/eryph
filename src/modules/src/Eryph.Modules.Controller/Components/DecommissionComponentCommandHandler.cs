using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
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

        var removed = await registry.RevokeAsync(message.ComponentId, CancellationToken.None);
        logger.LogInformation(
            "Decommissioned component {ComponentId} (broker user removed; registration removed: {Removed}).",
            message.ComponentId, removed);
    }
}
