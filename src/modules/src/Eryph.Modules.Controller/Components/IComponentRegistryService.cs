using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Maintains the controller's <see cref="ComponentRegistration"/> catalog from
/// registration, heartbeat and config-applied messages.
/// </summary>
internal interface IComponentRegistryService
{
    Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes liveness from a periodic heartbeat and reconciles the recorded
    /// applied-config state with what the component reports. The heartbeat is the
    /// component's authoritative current state: a restart (signalled by a new
    /// <paramref name="instanceId"/>) resets <paramref name="appliedConfigVersions"/>,
    /// so this overwrites rather than merges. Returns the updated registration, or
    /// <c>null</c> when the component is not registered or the heartbeat is from a
    /// superseded instance — in which case nothing is recorded and the caller must not
    /// act on it (e.g. drift reconciliation).
    /// </summary>
    Task<ComponentRegistration?> RecordHeartbeatAsync(
        Guid componentId,
        Guid instanceId,
        IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that a component applied a configuration version. Monotonic per
    /// domain: an older or duplicate version is ignored, so a late acknowledgement
    /// can never regress the recorded state.
    /// </summary>
    Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a component's registration on its graceful shutdown, so it leaves the catalog
    /// immediately instead of being aged out after the heartbeat timeout. Guarded by
    /// <paramref name="instanceId"/>: a late message from a previous run does not remove the
    /// registration a restarted instance already replaced. Returns whether a row was removed.
    /// </summary>
    Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a component's registration unconditionally on an operator decommission — the explicit
    /// "this component is gone for good" action, as opposed to the instance-guarded graceful
    /// <see cref="DeregisterAsync"/>. Not instance-scoped: decommissioning revokes the identity
    /// regardless of which run last registered it. Returns whether a row was removed.
    /// </summary>
    Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns operator-owned targeting metadata (environment + tags) to a registered component.
    /// Replaces the environment and the full tag set. Returns whether the component exists.
    /// </summary>
    Task<bool> SetMetadataAsync(
        Guid componentId,
        string? environment,
        IReadOnlyDictionary<string, string> tags,
        CancellationToken cancellationToken);

    /// <summary>The registration for a component, or <c>null</c> when it is not registered.</summary>
    Task<ComponentRegistration?> GetAsync(Guid componentId, CancellationToken cancellationToken);

    /// <summary>The components currently considered alive (status Active).</summary>
    Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken);
}
