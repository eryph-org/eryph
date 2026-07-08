using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Operation handler that assigns operator-owned targeting metadata (environment + tags) to a
/// registered component. Fails the operation when the component is not registered.
/// </summary>
[UsedImplicitly]
internal sealed class SetComponentMetadataCommandHandler(
    IBus bus,
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

        // The new scope can select a different authored value for the component's authorable domains, so
        // re-distribute them. The refresh re-evaluates each domain per component and only pushes to those
        // whose resolved value actually changed, so refreshing every authorable entitled domain is safe.
        var registration = await registry.GetAsync(command.ComponentId, CancellationToken.None);
        if (registration is not null)
        {
            var authorableDomains = ComponentConfigEntitlements
                .GetEntitledDomains(registration.ComponentType)
                .Where(ConfigDomainDescriptors.IsAuthorable);
            foreach (var domain in authorableDomains)
                await bus.Advanced.Routing.Send(
                    QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = domain });
        }

        await messaging.CompleteTask(message);
    }
}
