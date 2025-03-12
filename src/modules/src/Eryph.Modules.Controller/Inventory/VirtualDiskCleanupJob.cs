using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;
using Quartz;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Inventory;

internal class VirtualDiskCleanupJob(Container container) : IJob
{
    private const int MaxRounds = 5;

    public static readonly JobKey Key = new(nameof(VirtualDiskCleanupJob));
    private readonly ILogger _logger = container.GetInstance<ILogger<VirtualDiskCleanupJob>>();

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        _logger.LogDebug("Cleaning up disk entries which were deleted before {Cutoff:O}...", cutoff);
        try
        {
            // Perform multiple rounds of cleanup so the parent is removed
            // when all children were removed in previous rounds.
            // Limit the number of cleanup rounds to prevent the job from running
            // too long or forever in case of weird issues. This job is triggered
            // regularly by the scheduler anyway
            for (var i = 0; i < MaxRounds; i++)
            {
                if (!await CleanupDisks(cutoff))
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup deleted disks entries.");
        }
    }

    private async Task<bool> CleanupDisks(DateTimeOffset cutoff)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var stateStore = container.GetInstance<IStateStore>();
        var lockManager = container.GetInstance<IInventoryLockManager>();

        var disks = await stateStore.For<VirtualDisk>().ListAsync(
            new VirtualDiskSpecs.FindDeletedWithoutChildren(cutoff));
        if (disks.Count == 0)
            return false;

        _logger.LogDebug("Removing {Count} deleted disk entries...", disks.Count);
        var diskIdentifiers = disks.Select(d => d.DiskIdentifier).Distinct().Order();
        foreach (var diskIdentifier in diskIdentifiers)
        {
            await lockManager.AcquireVhdLock(diskIdentifier);
        }

        await stateStore.For<VirtualDisk>().DeleteRangeAsync(disks);
        await stateStore.SaveChangesAsync();
        
        return true;
    }
}
