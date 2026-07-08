using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
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
    INetworkSyncService networkSyncService,
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

        var scope = command.Scope ?? ConfigScope.Default;
        if (!ConfigScope.IsValid(scope))
        {
            await messaging.FailTask(message, $"'{command.Scope}' is not a valid configuration scope.");
            return;
        }

        // A domain that is not per-scope (e.g. the single global network topology) must be authored at
        // the default scope only — its source and consumers read the default value, so a non-default
        // scope would be stored and resolved but never actually distributed. Reject it loudly.
        if (scope != ConfigScope.Default && !ConfigDomainDescriptors.SupportsScopedAuthoring(command.Domain))
        {
            await messaging.FailTask(message,
                $"The {command.Domain} domain can only be authored at the default scope.");
            return;
        }

        if (!ConfigDomainDescriptors.TryCanonicalize(
                command.Domain, command.Payload ?? "", out var canonical, out var error))
        {
            await messaging.FailTask(message,
                error ?? $"The payload is not a valid {command.Domain} configuration.");
            return;
        }

        await store.AddVersionAsync(
            command.Domain, scope, canonical, command.Author, CancellationToken.None);

        // NetworkProviders drives the controller's OWN network realization, so re-realize now against the
        // just-authored value (SyncNetworks reads it through the authored-aware provider manager). That
        // both applies it to OVN and re-evaluates + pushes NetworkProviders and OvnCluster to components,
        // so it replaces the plain refresh. A realization failure fails the operation — the version is
        // stored and will realize on the next sync, but the operator must know it did not apply now.
        if (command.Domain == ConfigDomain.NetworkProviders)
        {
            var syncError = await networkSyncService.SyncNetworks(CancellationToken.None)
                .Match(_ => "", error => error.Message);
            if (!string.IsNullOrEmpty(syncError))
            {
                await messaging.FailTask(message,
                    $"The network provider configuration was stored but could not be realized: {syncError}");
                return;
            }
        }
        else
        {
            // Re-evaluate the domain against its new authored value and push it to entitled components.
            // The refresh no-ops if the canonical content did not actually change.
            await bus.Advanced.Routing.Send(
                QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = command.Domain });
        }

        await messaging.CompleteTask(message);
    }
}
