using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Answers a component's startup config request: builds the role-scoped snapshot of
/// the domains it is entitled to and is missing, and replies to the requester.
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

        // Reply to the request's return address (the sender's own input queue) instead of a
        // queue named in the message: trusting a message field would let any bus actor have an
        // entitled snapshot delivered to an arbitrary queue. The controller-driven push path
        // (RefreshConfigDomainCommandHandler) uses the queue persisted at registration.
        await bus.Reply(new ConfigSnapshotCommand
        {
            ComponentId = message.ComponentId,
            Bundles = bundles,
        });
    }
}
