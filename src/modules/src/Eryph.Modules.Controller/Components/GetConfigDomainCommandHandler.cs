using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Answers a request for a domain's current authored value (the highest version), replying with empty
/// version/payload when nothing has been authored yet. Read side of the config-management API.
/// </summary>
[UsedImplicitly]
internal sealed class GetConfigDomainCommandHandler(
    IBus bus,
    IAuthoredConfigStore store)
    : IHandleMessages<GetConfigDomainCommand>
{
    public async Task Handle(GetConfigDomainCommand message)
    {
        var current = await store.GetCurrentAsync(
            message.Domain, ConfigScope.Default, CancellationToken.None);

        // Reply to the request's return address; the requester correlates the response.
        await bus.Reply(new ConfigDomainResponse
        {
            Domain = message.Domain,
            Version = current?.Version,
            Payload = current?.Payload,
        });
    }
}
