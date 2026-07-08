using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Refreshes a component's liveness from its periodic heartbeat and reconciles configuration drift.
/// The targeted push in <see cref="RefreshConfigDomainCommandHandler"/> is fire-and-forget with no
/// retry of its own, so a component that was unreachable when a domain changed — or whose apply
/// failed — would otherwise run stale config indefinitely. On each heartbeat the component reports
/// what it has applied; if that is behind the authoritative records, the newer bundles are re-sent to
/// its inbound queue. This is the self-healing safety net of the distribution loop. A failed apply
/// never advances the reported version, so it is retried on the next heartbeat too.
/// </summary>
[UsedImplicitly]
internal sealed class ComponentHeartbeatCommandHandler(
    IBus bus,
    IComponentRegistryService registry,
    ConfigDistributionService distribution)
    : IHandleMessages<ComponentHeartbeatCommand>
{
    public async Task Handle(ComponentHeartbeatCommand message)
    {
        var registration = await registry.RecordHeartbeatAsync(
            message.ComponentId,
            message.InstanceId,
            message.AppliedConfigVersions,
            CancellationToken.None);

        // Only the live, matching instance drives reconciliation: a heartbeat from an unregistered
        // or superseded instance records nothing and returns null.
        if (registration is null)
            return;

        // NOTE (pre-auth trust boundary): like RequestConfigCommandHandler, the heartbeat's component
        // identity is not yet verified against an authenticated sender. A forged heartbeat carrying a
        // real component/instance id and a low applied version can therefore now trigger a drift re-push
        // on that component's heartbeat interval — an escalation from the passive registry update this
        // used to be. The push still only targets the queue persisted at registration (never a message
        // field), so it cannot be redirected or leak config to a new party; binding the heartbeat to an
        // authenticated component is part of the component authentication phase.
        // Reconcile against the applied state as recorded (the returned registration), not the raw
        // message, so drift detection stays aligned with whatever the registry accepted.
        var outdated = await distribution.GetOutdatedBundlesAsync(
            registration, registration.AppliedConfigVersions, CancellationToken.None);
        if (outdated.Count == 0)
            return;

        // Route to the queue persisted at registration (never a message field) so a drift push cannot be
        // redirected. ConfigSnapshotCommand (not the single-domain PushConfigCommand the refresh path
        // uses) carries every outdated domain in one send; the component applies it exactly like the
        // startup snapshot reply.
        await bus.Advanced.Routing.Send(registration.InboundQueue, new ConfigSnapshotCommand
        {
            ComponentId = registration.ComponentId,
            Bundles = outdated,
        });
    }
}
