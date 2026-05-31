using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Periodically sends a heartbeat to the controller, echoing the applied
/// configuration versions so the controller can refresh liveness and detect drift.
/// </summary>
internal sealed class ComponentHeartbeatService(
    IBus bus,
    ComponentIdentity identity,
    IComponentConfigState state,
    ILogger<ComponentHeartbeatService> logger)
    : IHostedService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _loop = RunAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop; }
            catch (OperationCanceledException) { }
        }

        // The loop has finished; dispose the CTS so its wait handle is not leaked for the
        // remainder of the process lifetime.
        _stopping.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, cancellationToken);
                await bus.Advanced.Routing.Send(QueueNames.Controllers, new ComponentHeartbeatCommand
                {
                    ComponentId = identity.ComponentId,
                    InstanceId = identity.InstanceId,
                    AppliedConfigVersions = state.GetApplied().ToDictionary(kv => kv.Key, kv => kv.Value),
                    Timestamp = DateTimeOffset.UtcNow,
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send component heartbeat.");
            }
        }
    }
}
