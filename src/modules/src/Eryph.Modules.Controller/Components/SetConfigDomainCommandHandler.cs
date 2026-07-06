using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Stores a new operator-authored version of a configuration domain and re-distributes it. The payload
/// is validated by the management API before it is sent (and, going forward, by a per-domain validator
/// on this side); here it is persisted as the authored authority — history preserved — and a refresh is
/// triggered so entitled components pick up the new version.
/// </summary>
[UsedImplicitly]
internal sealed class SetConfigDomainCommandHandler(
    IBus bus,
    IAuthoredConfigStore store)
    : IHandleMessages<SetConfigDomainCommand>
{
    public async Task Handle(SetConfigDomainCommand message)
    {
        if (string.IsNullOrEmpty(message.Payload))
            throw new InvalidOperationException(
                $"A configuration payload is required to set the {message.Domain} domain.");

        await store.AddVersionAsync(
            message.Domain, ConfigScope.Default, message.Payload, message.Author, CancellationToken.None);

        // Re-evaluate the domain against its new authored value and push it to entitled components.
        // The refresh no-ops if the serialized content did not actually change.
        await bus.Advanced.Routing.Send(
            QueueNames.Controllers, new RefreshConfigDomainCommand { Domain = message.Domain });
    }
}
