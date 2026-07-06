using System.Threading.Tasks;
using Eryph.Messages.Components;
using Rebus.Handlers;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Applies a configuration snapshot the controller sends to this component's inbound queue: either the
/// reply to the component's startup config request, or a heartbeat-driven drift re-push (from
/// <c>ComponentHeartbeatCommandHandler</c>) when the component is found behind the authoritative records.
/// </summary>
internal sealed class ConfigSnapshotCommandHandler(
    ComponentIdentity identity,
    ConfigApplier applier)
    : IHandleMessages<ConfigSnapshotCommand>
{
    public async Task Handle(ConfigSnapshotCommand message)
    {
        // The component's queue is its own; ignore anything not addressed to it.
        if (message.ComponentId != identity.ComponentId)
            return;

        foreach (var bundle in message.Bundles)
            await applier.ApplyAsync(bundle);
    }
}
