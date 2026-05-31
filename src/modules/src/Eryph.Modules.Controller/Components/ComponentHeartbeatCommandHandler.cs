using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Refreshes a component's liveness from its periodic heartbeat.
/// </summary>
[UsedImplicitly]
internal sealed class ComponentHeartbeatCommandHandler(
    IComponentRegistryService registry)
    : IHandleMessages<ComponentHeartbeatCommand>
{
    public Task Handle(ComponentHeartbeatCommand message) =>
        registry.RecordHeartbeatAsync(
            message.ComponentId,
            message.InstanceId,
            message.AppliedConfigVersions,
            CancellationToken.None);
}
