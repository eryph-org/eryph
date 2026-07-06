using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Stores a new operator-authored version of a configuration domain and re-distributes it, replying
/// with the outcome. Only <c>Authorable</c> domains (see <see cref="ConfigDomainDescriptors"/>) may be
/// set, and the payload is validated against the domain's schema before it is stored — a malformed or
/// wrong-domain write is rejected here, not distributed and left to wedge or silently empty the fleet.
/// </summary>
[UsedImplicitly]
internal sealed class SetConfigDomainCommandHandler(
    IBus bus,
    IAuthoredConfigStore store)
    : IHandleMessages<SetConfigDomainCommand>
{
    public async Task Handle(SetConfigDomainCommand message)
    {
        // NOTE (pre-auth trust boundary): like RequestConfigCommandHandler and
        // ComponentHeartbeatCommandHandler, the sender is not yet authenticated, so any bus actor can
        // author configuration that is persisted and pushed to every entitled component. This is the
        // most powerful of those pre-auth paths; restricting authoring to the management component is
        // part of the component authentication phase. Until then the guards below keep a wrong-domain or
        // malformed write from corrupting a domain, but not an authorized-but-hostile one.
        if (!ConfigDomainDescriptors.IsAuthorable(message.Domain))
        {
            await Reply(false, null, $"The {message.Domain} domain is system-derived and cannot be authored.");
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Payload)
            || !ConfigDomainDescriptors.IsValidPayload(message.Domain, message.Payload))
        {
            await Reply(false, null, $"The payload is not a valid {message.Domain} configuration.");
            return;
        }

        var entry = await store.AddVersionAsync(
            message.Domain, ConfigScope.Default, message.Payload, message.Author, CancellationToken.None);

        // Re-evaluate the domain against its new authored value and push it to entitled components.
        // The refresh no-ops if the serialized content did not actually change.
        await bus.Advanced.Routing.Send(
            QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = message.Domain });

        await Reply(true, entry.Version, null);
    }

    private Task Reply(bool success, long? version, string? error) =>
        bus.Reply(new SetConfigDomainResponse { Success = success, Version = version, Error = error });
}
