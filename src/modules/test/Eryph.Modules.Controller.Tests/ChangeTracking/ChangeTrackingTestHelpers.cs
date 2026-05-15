using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

internal static class ChangeTrackingTestHelpers
{
    // Poll until every registered change-tracking queue is empty, then briefly
    // re-check to ensure no handler is still in flight. Used by tests to avoid
    // racing host.StopAsync against a BackgroundService whose ExecuteAsync has
    // not yet been scheduled by the thread pool.
    //
    // The queue list is derived from the ChangeTrackingBackgroundService<>
    // hosted services registered on the host. Adding another change type
    // (one more AddHostedService<ChangeTrackingBackgroundService<TNew>>() in
    // ChangeTrackingContainerExtensions) is picked up automatically; no
    // update to this helper is required.
    public static async Task WaitForIdleAsync(IHost host, TimeSpan timeout)
    {
        var queues = ResolveQueues(host);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (queues.All(q => q.GetCount() == 0))
            {
                await Task.Delay(20);
                if (queues.All(q => q.GetCount() == 0))
                    return;
            }
            await Task.Delay(10);
        }

        var counts = string.Join(", ", queues.Select(q => $"{q.ChangeTypeName}={q.GetCount()}"));
        throw new TimeoutException($"Change tracking queues did not drain in time. Counts: {counts}");
    }

    private static IReadOnlyList<QueueProbe> ResolveQueues(IHost host)
    {
        var container = host.Services.GetRequiredService<Container>();
        return host.Services.GetServices<IHostedService>()
            .Select(s => s.GetType())
            .Where(t => t.IsGenericType
                        && t.GetGenericTypeDefinition() == typeof(ChangeTrackingBackgroundService<>))
            .Select(t =>
            {
                var changeType = t.GetGenericArguments()[0];
                var queueType = typeof(IChangeTrackingQueue<>).MakeGenericType(changeType);
                var queue = container.GetInstance(queueType);
                var getCount = (Func<int>)Delegate.CreateDelegate(
                    typeof(Func<int>),
                    queue,
                    queueType.GetMethod(nameof(IChangeTrackingQueue<object>.GetCount))!);
                return new QueueProbe(changeType.Name, getCount);
            })
            .ToList();
    }

    private sealed record QueueProbe(string ChangeTypeName, Func<int> GetCount);
}
