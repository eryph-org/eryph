using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Persists a component's registration (the service catalog + liveness). The
/// component requests its configuration separately via <see cref="RequestConfigCommand"/>.
/// </summary>
[UsedImplicitly]
internal sealed class RegisterComponentCommandHandler(
    IComponentRegistryService registry,
    ILogger<RegisterComponentCommandHandler> logger)
    : IHandleMessages<RegisterComponentCommand>
{
    public async Task Handle(RegisterComponentCommand message)
    {
        await registry.UpsertAsync(message, CancellationToken.None);
        logger.LogInformation(
            "Registered component {ComponentType} ({ComponentId}) on queue {Queue}.",
            message.ComponentType, message.ComponentId, message.InboundQueue);
    }
}
