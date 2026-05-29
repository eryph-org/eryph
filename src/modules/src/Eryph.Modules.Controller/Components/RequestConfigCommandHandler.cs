using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Answers a component's startup config request: builds the role-scoped snapshot of
/// the domains it is entitled to and is missing, and routes it to the component's
/// inbound queue.
/// </summary>
[UsedImplicitly]
internal sealed class RequestConfigCommandHandler(
    IBus bus,
    ConfigDistributionService distribution)
    : IHandleMessages<RequestConfigCommand>
{
    public async Task Handle(RequestConfigCommand message)
    {
        var bundles = await distribution.BuildSnapshotAsync(
            message.ComponentType, message.KnownConfigVersions, CancellationToken.None);

        if (bundles.Count == 0)
            return;

        await bus.Advanced.Routing.Send(message.InboundQueue, new ConfigSnapshotCommand
        {
            ComponentId = message.ComponentId,
            Bundles = bundles,
        });
    }
}
