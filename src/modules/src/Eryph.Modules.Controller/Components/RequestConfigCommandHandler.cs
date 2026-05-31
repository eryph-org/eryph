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
        // NOTE (pre-auth trust boundary): the requester's ComponentType is taken from the message
        // and not yet verified against an authenticated identity, so the entitlement is only as
        // trustworthy as the bus. Binding the request to an authenticated component — so a bus
        // actor cannot claim a more privileged ComponentType — is part of the component
        // authentication phase; deriving it from the registration would not help until then,
        // since an unauthenticated actor could forge the registration too.
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
