using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Operation handler that assigns operator-owned targeting metadata (environment + tags) to a
/// registered component. Fails the operation when the component is not registered.
/// </summary>
[UsedImplicitly]
internal sealed class SetComponentMetadataCommandHandler(
    IComponentRegistryService registry,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<SetComponentMetadataCommand>>
{
    public async Task Handle(OperationTask<SetComponentMetadataCommand> message)
    {
        var command = message.Command;

        var found = await registry.SetMetadataAsync(
            command.ComponentId, command.Environment, command.Tags, CancellationToken.None);

        if (!found)
        {
            await messaging.FailTask(message, $"Component {command.ComponentId} is not registered.");
            return;
        }

        await messaging.CompleteTask(message);
    }
}
