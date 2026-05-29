using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Re-evaluates a configuration domain when its source changed and, if the
/// content actually changed (version bumped), pushes the new bundle to every live
/// component entitled to the domain.
/// </summary>
[UsedImplicitly]
internal sealed class RefreshConfigDomainCommandHandler(
    IBus bus,
    ConfigDistributionService distribution,
    IComponentRegistryService registry,
    ILogger<RefreshConfigDomainCommandHandler> logger)
    : IHandleMessages<RefreshConfigDomainCommand>
{
    public async Task Handle(RefreshConfigDomainCommand message)
    {
        var bundle = await distribution.RefreshAsync(message.Domain, CancellationToken.None);
        if (bundle is null)
            return; // content unchanged — nothing to publish

        var components = await registry.GetActiveAsync(CancellationToken.None);
        var recipients = 0;
        foreach (var component in components)
        {
            if (!distribution.GetEntitledDomains(component.ComponentType).Contains(message.Domain))
                continue;

            await bus.Advanced.Routing.Send(component.InboundQueue, new PushConfigCommand
            {
                ComponentId = component.ComponentId,
                Bundle = bundle,
            });
            recipients++;
        }

        logger.LogInformation(
            "Published {Domain} version {Version} to {Recipients} subscriber(s).",
            message.Domain, bundle.Version, recipients);
    }
}
