using Eryph.Core;
using Eryph.Modules.Controller.Inventory;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Xunit.Abstractions;

namespace Eryph.Modules.Controller.Tests;

public class OperationCleanupJobTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private static readonly OperationsHousekeepingConfig Config = new()
    {
        RetentionAge = TimeSpan.FromDays(30),
        RunningTimeout = TimeSpan.FromDays(1),
    };

    private static readonly Guid ExpiredOpId = Guid.NewGuid();
    private static readonly Guid TimedOutOpId = Guid.NewGuid();
    private static readonly Guid FreshOpId = Guid.NewGuid();
    private static readonly Guid RunningTaskId = Guid.NewGuid();
    private static readonly Guid DoneTaskId = Guid.NewGuid();

    private Container _container = null!;

    protected override void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        _container = options.Container;
        options.Container.RegisterInstance(Config);
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        var now = DateTimeOffset.UtcNow;

        // Completed and older than the retention age: must be deleted.
        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = ExpiredOpId,
            TenantId = EryphConstants.DefaultTenantId,
            Status = OperationStatus.Completed,
            Created = now.AddDays(-40),
            LastUpdated = now.AddDays(-40),
        });

        // Still running but stale past the timeout: must be failed (not deleted).
        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = TimedOutOpId,
            TenantId = EryphConstants.DefaultTenantId,
            Status = OperationStatus.Running,
            Created = now.AddDays(-2),
            LastUpdated = now.AddDays(-2),
            Tasks =
            [
                new OperationTaskModel
                {
                    Id = RunningTaskId,
                    Status = OperationTaskStatus.Running,
                    Created = now.AddDays(-2),
                    LastUpdated = now.AddDays(-2),
                    Progress = [],
                },
                new OperationTaskModel
                {
                    Id = DoneTaskId,
                    Status = OperationTaskStatus.Completed,
                    Created = now.AddDays(-2),
                    LastUpdated = now.AddDays(-2),
                    EndedAt = now.AddDays(-2),
                    Progress = [],
                },
            ],
        });

        // Fresh running operation: must be left untouched.
        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = FreshOpId,
            TenantId = EryphConstants.DefaultTenantId,
            Status = OperationStatus.Running,
            Created = now,
            LastUpdated = now,
        });
    }

    [Fact]
    public async Task Execute_deletes_expired_and_fails_timed_out_operations()
    {
        var job = new OperationCleanupJob(_container);
        await job.Execute(null!);

        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        // The expired operation has been deleted.
        (await stateStore.For<OperationModel>().GetByIdAsync(ExpiredOpId)).Should().BeNull();

        // The fresh operation is untouched.
        var fresh = await stateStore.For<OperationModel>().GetByIdAsync(FreshOpId);
        fresh!.Status.Should().Be(OperationStatus.Running);
        fresh.EndedAt.Should().BeNull();

        // The timed out operation has been failed.
        var timedOut = await stateStore.For<OperationModel>().GetByIdAsync(TimedOutOpId);
        timedOut!.Status.Should().Be(OperationStatus.Failed);
        timedOut.StatusMessage.Should().Be("Operation timed out.");
        timedOut.EndedAt.Should().NotBeNull();

        // Its running task has been failed as well...
        var runningTask = await stateStore.For<OperationTaskModel>().GetByIdAsync(RunningTaskId);
        runningTask!.Status.Should().Be(OperationTaskStatus.Failed);
        runningTask.EndedAt.Should().NotBeNull();

        // ...but its already completed task is left as it was.
        var doneTask = await stateStore.For<OperationTaskModel>().GetByIdAsync(DoneTaskId);
        doneTask!.Status.Should().Be(OperationTaskStatus.Completed);
    }
}
