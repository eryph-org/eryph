using System;
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

    Task RecordHeartbeatAsync(Guid componentId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that a component applied a configuration version. Monotonic per
    /// domain: an older or duplicate version is ignored, so a late acknowledgement
    /// can never regress the recorded state.
    /// </summary>
    Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version, CancellationToken cancellationToken);
}
