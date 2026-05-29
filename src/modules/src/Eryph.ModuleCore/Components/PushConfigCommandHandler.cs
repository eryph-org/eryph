using System.Threading.Tasks;
using Eryph.Messages.Components;
using Rebus.Handlers;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Applies a single configuration bundle the controller pushes when a domain the
/// component holds changes.
/// </summary>
internal sealed class PushConfigCommandHandler(
    ComponentIdentity identity,
    ConfigApplier applier)
    : IHandleMessages<PushConfigCommand>
{
    public async Task Handle(PushConfigCommand message)
    {
        if (message.ComponentId != identity.ComponentId)
            return;

        await applier.ApplyAsync(message.Bundle);
    }
}
