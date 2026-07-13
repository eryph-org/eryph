using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.HostAgent.Inventory;

public interface IProvisioningStateMonitor
{
    /// <summary>
    /// Starts actively polling the provisioning status of the given catlet. Has
    /// no effect if the catlet is already tracked.
    /// </summary>
    void Track(Guid vmId);

    /// <summary>
    /// Stops actively polling the provisioning status of the given catlet.
    /// </summary>
    void Untrack(Guid vmId);
}

/// <summary>
/// Actively polls the provisioning status of catlets going through their first
/// boot and reports changes to the controller via
/// <see cref="CatletProvisioningStatusChangedEvent"/>.
/// </summary>
/// <remarks>
/// Provisioning is only meaningful during the first boot, so a catlet is
/// enrolled when it enters the running state (see
/// <see cref="ProvisioningMonitorStateChangedHandler"/>) and dropped as soon as
/// provisioning reaches a terminal state, the catlet leaves the running state,
/// or a safety timeout elapses. Once dropped, the regular inventory keeps the
/// persisted status reconciled.
/// </remarks>
internal sealed class ProvisioningStateMonitor(
    IProvisioningStateReader reader,
    IBus bus,
    WorkflowOptions workflowOptions,
    ILogger logger)
    : BackgroundService, IProvisioningStateMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // A guest that never reports a terminal provisioning state (e.g. one without
    // provisioning reporting) must not be polled forever. After this window we
    // stop actively polling and leave the status to the regular inventory.
    private static readonly TimeSpan MaxTrackingDuration = TimeSpan.FromMinutes(30);

    private sealed record TrackedVm(DateTimeOffset EnrolledAt, ProvisioningStatus LastReported);

    private readonly ConcurrentDictionary<Guid, TrackedVm> _tracked = new();

    public void Track(Guid vmId) =>
        _tracked.AddOrUpdate(
            vmId,
            _ => new TrackedVm(DateTimeOffset.UtcNow, ProvisioningStatus.Unknown),
            (_, existing) => existing);

    public void Untrack(Guid vmId) => _tracked.TryRemove(vmId, out _);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            foreach (var (vmId, tracked) in _tracked.ToArray())
            {
                try
                {
                    await PollAsync(vmId, tracked, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex,
                        "Failed to poll the provisioning status of VM {VmId}.", vmId);
                }
            }
        }
    }

    private async Task PollAsync(Guid vmId, TrackedVm tracked, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow - tracked.EnrolledAt > MaxTrackingDuration)
        {
            logger.LogDebug(
                "Stopping provisioning monitoring of VM {VmId} after the tracking timeout elapsed.",
                vmId);
            _tracked.TryRemove(vmId, out _);
            return;
        }

        var status = await reader.ReadAsync(vmId).ConfigureAwait(false);

        // Unknown means guest-services has not reported a state (yet). Keep
        // waiting rather than pushing a status that would regress the DB.
        if (status is ProvisioningStatus.Unknown || status == tracked.LastReported)
            return;

        await bus.SendWorkflowEvent(workflowOptions, new CatletProvisioningStatusChangedEvent
        {
            VmId = vmId,
            Status = status,
            Timestamp = DateTimeOffset.UtcNow,
        }).ConfigureAwait(false);

        logger.LogDebug(
            "Reported provisioning status {Status} for VM {VmId}.", status, vmId);

        // Terminal states end monitoring; otherwise remember what we reported so
        // we only push on the next change.
        if (status is ProvisioningStatus.Completed or ProvisioningStatus.Failed)
            _tracked.TryRemove(vmId, out _);
        else
            _tracked.TryUpdate(vmId, tracked with { LastReported = status }, tracked);
    }
}
