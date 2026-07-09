using System.Collections.Generic;
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

        // Validate that the environment and tags produce well-formed, in-length scope selectors — the
        // same canonicalization the authoring path applies. Without this an over-long or malformed value
        // is stored (Environment/Tags are unbounded text columns) but the derived selector exceeds the
        // 255-char Scope column and can never match an authored value, silently making the component
        // un-targetable at its resolved scope with no operator-visible error.
        if (!string.IsNullOrWhiteSpace(command.Environment)
            && !ConfigScope.TryCanonicalize("env:" + command.Environment, out _, out var envError))
        {
            await messaging.FailTask(message, envError!);
            return;
        }

        foreach (var tag in command.Tags ?? new Dictionary<string, string?>())
        {
            // Validate the raw key first: reconstructing "tag:key=value" and canonicalizing splits on the
            // first '=', so a key containing '=' would otherwise be silently reinterpreted as part of the
            // value instead of being rejected. Then check the whole selector's form and length.
            if (!ConfigScope.IsValidTagKey(tag.Key, out var tagKeyError))
            {
                await messaging.FailTask(message, tagKeyError!);
                return;
            }

            if (!ConfigScope.TryCanonicalize($"tag:{tag.Key}={tag.Value}", out _, out var tagError))
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
