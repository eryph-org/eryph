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

        // Reject tag keys that would produce an ambiguous scope selector before storing the metadata.
        foreach (var key in command.Tags?.Keys ?? Enumerable.Empty<string>())
        {
            if (!ConfigScope.IsValidTagKey(key, out var tagError))
            {
                await messaging.FailTask(message, tagError!);
                return;
            }
        }

        var found = await registry.SetMetadataAsync(
            command.ComponentId, command.Environment, command.Tags, CancellationToken.None);

        if (!found)
        {
            await messaging.FailTask(message, $"Component {command.ComponentId} is not registered.");
            return;
        }

        // The new scope can select a different authored value for the component's scoped domains, so
        // re-distribute them. Only per-scope domains are affected — a default-scope-only domain (e.g.
        // NetworkProviders) resolves the same value regardless of environment/tags, so refreshing it
        // would be a guaranteed no-op. The refresh re-evaluates per component and pushes only where the
        // resolved value actually changed.
        var registration = await registry.GetAsync(command.ComponentId, CancellationToken.None);
        if (registration is not null)
        {
            var scopedDomains = ComponentConfigEntitlements
                .GetEntitledDomains(registration.ComponentType)
                .Where(ConfigDomainDescriptors.SupportsScopedAuthoring);
            foreach (var domain in scopedDomains)
                await bus.Advanced.Routing.Send(
                    QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = domain });
        }

        await messaging.CompleteTask(message);
    }
}
