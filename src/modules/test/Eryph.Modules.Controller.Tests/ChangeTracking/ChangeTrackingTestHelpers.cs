using System;
using System.Threading.Tasks;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.ChangeTracking.Catlets;
using Eryph.Modules.Controller.ChangeTracking.NetworkProviders;
using Eryph.Modules.Controller.ChangeTracking.Projects;
using Eryph.Modules.Controller.ChangeTracking.VirtualMachines;
using Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;
using SimpleInjector;

namespace Eryph.Modules.Controller.Tests.ChangeTracking;

internal static class ChangeTrackingTestHelpers
{
    // Poll until every change-tracking queue is empty, then briefly re-check
    // to ensure no handler is still in flight. Used by tests to avoid racing
    // host.StopAsync against a BackgroundService whose ExecuteAsync has not
    // yet been scheduled by the thread pool.
    public static async Task WaitForIdleAsync(Container container, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (AllQueuesEmpty(container))
            {
                await Task.Delay(20);
                if (AllQueuesEmpty(container))
                    return;
            }
            await Task.Delay(10);
        }

        throw new TimeoutException(
            "Change tracking queues did not drain in time. Counts: " +
            $"VN={GetCount<VirtualNetworkChange>(container)}, " +
            $"NP={GetCount<NetworkProvidersChange>(container)}, " +
            $"Pr={GetCount<ProjectChange>(container)}, " +
            $"CM={GetCount<CatletMetadataChange>(container)}, " +
            $"CS={GetCount<CatletSpecificationChange>(container)}, " +
            $"CSV={GetCount<CatletSpecificationVersionChange>(container)}");
    }

    private static bool AllQueuesEmpty(Container c) =>
        GetCount<VirtualNetworkChange>(c) == 0
        && GetCount<NetworkProvidersChange>(c) == 0
        && GetCount<ProjectChange>(c) == 0
        && GetCount<CatletMetadataChange>(c) == 0
        && GetCount<CatletSpecificationChange>(c) == 0
        && GetCount<CatletSpecificationVersionChange>(c) == 0;

    private static int GetCount<TChange>(Container c) =>
        c.GetInstance<IChangeTrackingQueue<TChange>>().GetCount();
}
