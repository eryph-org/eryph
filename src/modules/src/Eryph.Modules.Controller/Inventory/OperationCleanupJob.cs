using System;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;
using Quartz;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Inventory;

/// <summary>
/// Housekeeping for operations: fails operations which are stuck (queued or
/// running past a timeout) and deletes operations which are older than the
/// configured retention age.
/// </summary>
internal class OperationCleanupJob(Container container) : IJob
{
    public static readonly JobKey Key = new(nameof(OperationCleanupJob));

    private readonly ILogger _logger = container.GetInstance<ILogger<OperationCleanupJob>>();
    private readonly OperationsHousekeepingConfig _config = container.GetInstance<OperationsHousekeepingConfig>();

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await FailTimedOutOperations();
            await DeleteExpiredOperations();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run operations housekeeping.");
        }
    }

    private async Task FailTimedOutOperations()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - _config.RunningTimeout;
        _logger.LogDebug("Failing operations which are still running but were not updated since {Cutoff:O}...", cutoff);

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var stateStore = container.GetInstance<IStateStore>();

        var operations = await stateStore.For<OperationModel>().ListAsync(
            new OperationSpecs.FindTimedOut(cutoff));
        if (operations.Count == 0)
            return;

        _logger.LogInformation("Failing {Count} timed out operations.", operations.Count);
        foreach (var operation in operations)
        {
            operation.Status = OperationStatus.Failed;
            operation.StatusMessage = "Operation timed out.";
            operation.EndedAt = now;
            operation.LastUpdated = now;

            foreach (var task in operation.Tasks)
            {
                if (task.Status is OperationTaskStatus.Completed or OperationTaskStatus.Failed)
                    continue;

                task.Status = OperationTaskStatus.Failed;
                task.EndedAt = now;
                task.LastUpdated = now;
            }
        }

        await stateStore.SaveChangesAsync();
    }

    private async Task DeleteExpiredOperations()
    {
        var cutoff = DateTimeOffset.UtcNow - _config.RetentionAge;
        _logger.LogDebug("Deleting operations which were not updated since {Cutoff:O}...", cutoff);

        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var stateStore = container.GetInstance<IStateStore>();

        var operations = await stateStore.For<OperationModel>().ListAsync(
            new OperationSpecs.FindExpired(cutoff));
        if (operations.Count == 0)
            return;

        _logger.LogInformation("Deleting {Count} expired operations.", operations.Count);
        await stateStore.For<OperationModel>().DeleteRangeAsync(operations);
        await stateStore.SaveChangesAsync();
    }
}
