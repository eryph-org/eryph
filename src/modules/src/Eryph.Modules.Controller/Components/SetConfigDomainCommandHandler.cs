using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Operation handler that stores a new operator-authored version of a configuration domain and
/// re-distributes it. Only <c>Authorable</c> domains may be set, and the payload is validated and
/// canonicalized against the domain's schema before storing — a wrong-domain or malformed write fails
/// the operation rather than being distributed to wedge or silently empty the fleet.
/// </summary>
[UsedImplicitly]
internal sealed class SetConfigDomainCommandHandler(
    IBus bus,
    IAuthoredConfigStore store,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<SetConfigDomainCommand>>
{
    public async Task Handle(OperationTask<SetConfigDomainCommand> message)
    {
        var command = message.Command;

        // NOTE (pre-auth trust boundary): authoring is authorized at the management API by the
        // management:write scope; the bus itself does not yet authenticate the sender, so restricting
        // this command to the management component is part of the component authentication phase.
        if (!ConfigDomainDescriptors.IsAuthorable(command.Domain))
        {
            await messaging.FailTask(message,
                $"The {command.Domain} domain is system-derived and cannot be authored.");
            return;
        }

        if (string.IsNullOrWhiteSpace(command.Payload)
            || !ConfigDomainDescriptors.TryCanonicalize(command.Domain, command.Payload, out var canonical))
        {
            await messaging.FailTask(message,
                $"The payload is not a valid {command.Domain} configuration.");
            return;
        }

        await store.AddVersionAsync(
            command.Domain, ConfigScope.Default, canonical, command.Author, CancellationToken.None);

        // Re-evaluate the domain against its new authored value and push it to entitled components.
        // The refresh no-ops if the canonical content did not actually change.
        await bus.Advanced.Routing.Send(
            QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = command.Domain });

        await messaging.CompleteTask(message);
    }
}
