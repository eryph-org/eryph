using System.Threading.Tasks;
using Eryph.Messages.Components;
using Rebus.Handlers;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Applies the initial (or delta) configuration snapshot the controller sends to
/// this component's inbound queue on registration.
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
