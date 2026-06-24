using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

internal sealed class ComponentRegistryService(
    IStateStoreRepository<ComponentRegistration> repository)
    : IComponentRegistryService
{
    public async Task<ComponentRegistration> UpsertAsync(
        RegisterComponentCommand command,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetBySpecAsync(
            new ComponentRegistrationSpecs.GetByComponentId(command.ComponentId), cancellationToken);

        if (existing is null)
        {
            var registration = new ComponentRegistration
            {
                Id = Guid.NewGuid(),
                ComponentId = command.ComponentId,
                ComponentType = command.ComponentType,
                InstanceId = command.InstanceId,
                MachineName = command.MachineName,
                Version = command.Version,
                InboundQueue = command.InboundQueue,
                Status = ComponentRegistrationStatus.Active,
                RegisteredAt = DateTimeOffset.UtcNow,
                LastHeartbeat = DateTimeOffset.UtcNow,
                AppliedConfigVersions = new Dictionary<ConfigDomain, long>(command.KnownConfigVersions),
                AdvertisedEndpoints = new Dictionary<string, string>(command.AdvertisedEndpoints),
            };
            await repository.AddAsync(registration, cancellationToken);
            return registration;
        }

        existing.ComponentType = command.ComponentType;
        existing.InstanceId = command.InstanceId;
        existing.MachineName = command.MachineName;
        existing.Version = command.Version;
        existing.InboundQueue = command.InboundQueue;
        existing.Status = ComponentRegistrationStatus.Active;
        existing.LastHeartbeat = DateTimeOffset.UtcNow;
        existing.AdvertisedEndpoints = new Dictionary<string, string>(command.AdvertisedEndpoints);
        foreach (var (domain, version) in command.KnownConfigVersions)
            if (!existing.AppliedConfigVersions.TryGetValue(domain, out var current) || version > current)
                existing.AppliedConfigVersions[domain] = version;

        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task RecordHeartbeatAsync(
        Guid componentId,
        Guid instanceId,
        IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions,
        CancellationToken cancellationToken)
    {
        var registration = await repository.GetBySpecAsync(
            new ComponentRegistrationSpecs.GetByComponentId(componentId), cancellationToken);
        if (registration is null)
            return;

        // Ignore heartbeats that do not belong to the currently-registered instance.
        // Registration is the authority for the live InstanceId; on a brokered transport a
        // delayed heartbeat from a previous process instance can arrive after a newer
        // registration, and applying it would revert the catalog (InstanceId + applied-config
        // state) to the stale instance. A restart re-registers with its new InstanceId first,
        // so the matching heartbeat still resets applied state as intended.
        if (registration.InstanceId != instanceId)
            return;

        registration.LastHeartbeat = DateTimeOffset.UtcNow;
        registration.Status = ComponentRegistrationStatus.Active;
        // The heartbeat reports the component's current applied state verbatim. On a
        // restart the component reports an empty/reset set, which must be reflected so
        // the controller's view does not lag behind reality.
        registration.AppliedConfigVersions = new Dictionary<ConfigDomain, long>(appliedConfigVersions);
        await repository.UpdateAsync(registration, cancellationToken);
    }

    public async Task RecordAppliedAsync(
        Guid componentId,
        ConfigDomain domain,
        long version,
        CancellationToken cancellationToken)
    {
        var registration = await repository.GetBySpecAsync(
            new ComponentRegistrationSpecs.GetByComponentId(componentId), cancellationToken);
        if (registration is null)
            return;

        var current = registration.AppliedConfigVersions.GetValueOrDefault(domain, 0);
        if (version <= current)
            return;

        registration.AppliedConfigVersions[domain] = version;
        await repository.UpdateAsync(registration, cancellationToken);
    }

    // Liveness is evaluated at read time: a registration whose last heartbeat is older than the
    // timeout is treated as inactive even though its stored Status is still Active. Every component
    // (including eryph-zero's in-process modules) heartbeats on ComponentRegistrationDefaults
    // .HeartbeatInterval, so a live one is at most that stale while a crashed or removed component
    // (e.g. a network module that was split out and no longer runs here) ages out. Without this, a
    // stale Network registration would still be returned and the OVN northbound provider could
    // misdetect itself as co-located and dial the local pipe instead of the real remote database.
    // Read-time filtering is also self-healing — a component that missed a beat reappears on its
    // next heartbeat. (An explicit Stale/Dead status transition for health reporting, and ceasing
    // to push config to dead queues, remain follow-up #380 work.)
    public async Task<bool> DeregisterAsync(
        Guid componentId,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var registration = await repository.GetBySpecAsync(
            new ComponentRegistrationSpecs.GetByComponentId(componentId), cancellationToken);
        // Only the instance that is leaving may deregister: a late message from a previous run must
        // not remove the registration a restarted instance already replaced (different InstanceId),
        // mirroring the heartbeat guard above.
        if (registration is null || registration.InstanceId != instanceId)
            return false;

        await repository.DeleteAsync(registration, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken cancellationToken)
    {
        var registration = await repository.GetBySpecAsync(
            new ComponentRegistrationSpecs.GetByComponentId(componentId), cancellationToken);
        // Unconditional (not instance-guarded): an operator decommission removes the component whatever
        // its current instance — the broker-user deletion that accompanies it is the real revocation,
        // and leaving a stale row behind would keep advertising a component that can no longer connect.
        if (registration is null)
            return false;

        await repository.DeleteAsync(registration, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - ComponentRegistrationDefaults.HeartbeatTimeout;
        var active = await repository.ListAsync(new ComponentRegistrationSpecs.GetActive(), cancellationToken);
        return active.Where(r => r.LastHeartbeat >= cutoff).ToList();
    }
}
