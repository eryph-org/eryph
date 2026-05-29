using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Records a component's acknowledgement that it applied a configuration version
/// (monotonic per domain), or logs a failure.
/// </summary>
[UsedImplicitly]
internal sealed class ConfigAppliedEventHandler(
    IComponentRegistryService registry,
    ILogger<ConfigAppliedEventHandler> logger)
    : IHandleMessages<ConfigAppliedEvent>
{
    public async Task Handle(ConfigAppliedEvent message)
    {
        if (!message.Success)
        {
            logger.LogWarning(
                "Component {ComponentId} failed to apply {Domain} version {Version}: {Error}",
                message.ComponentId, message.Domain, message.Version, message.Error);
            return;
        }

        await registry.RecordAppliedAsync(
            message.ComponentId, message.Domain, message.Version, CancellationToken.None);
    }
}
