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
    /// so this overwrites rather than merges. Does nothing if the component is not
    /// registered.
    /// </summary>
    Task RecordHeartbeatAsync(
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

    /// <summary>The components currently considered alive (status Active).</summary>
    Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken);
}
