using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
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
                AppliedConfigVersions = new(command.KnownConfigVersions),
            };
            await repository.AddAsync(registration, cancellationToken);
            return registration;
        }

        existing.InstanceId = command.InstanceId;
        existing.MachineName = command.MachineName;
        existing.Version = command.Version;
        existing.InboundQueue = command.InboundQueue;
        existing.Status = ComponentRegistrationStatus.Active;
        existing.LastHeartbeat = DateTimeOffset.UtcNow;
        foreach (var (domain, version) in command.KnownConfigVersions)
        {
            if (!existing.AppliedConfigVersions.TryGetValue(domain, out var current) || version > current)
                existing.AppliedConfigVersions[domain] = version;
        }

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

        registration.LastHeartbeat = DateTimeOffset.UtcNow;
        registration.Status = ComponentRegistrationStatus.Active;
        registration.InstanceId = instanceId;
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

        var current = registration.AppliedConfigVersions.TryGetValue(domain, out var v) ? v : 0;
        if (version <= current)
            return;

        registration.AppliedConfigVersions[domain] = version;
        await repository.UpdateAsync(registration, cancellationToken);
    }

    public async Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken) =>
        await repository.ListAsync(new ComponentRegistrationSpecs.GetActive(), cancellationToken);
}
