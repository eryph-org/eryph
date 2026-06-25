using System.Linq;
using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.StateDb.Tests;

/// <summary>
/// <see cref="OperationTaskSpecs.FindActiveRouted"/> selects exactly the tasks a
/// cancellation should be delivered to: the operation's still-active tasks that have
/// been routed to a host. The dispatcher then sends to their distinct destinations.
/// </summary>
public class OperationTaskRoutingSpecTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private static readonly Guid OperationId = Guid.NewGuid();
    private static readonly Guid OtherOperationId = Guid.NewGuid();

    private static readonly Guid RunningA1 = Guid.NewGuid();
    private static readonly Guid RunningA2 = Guid.NewGuid();
    private static readonly Guid QueuedB = Guid.NewGuid();
    private static readonly Guid CompletedC = Guid.NewGuid();
    private static readonly Guid RunningUnrouted = Guid.NewGuid();

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = OperationId,
            TenantId = EryphConstants.DefaultTenantId,
            Status = OperationStatus.Running,
            Tasks =
            [
                Task(RunningA1, OperationTaskStatus.Running, "queue-a"),
                Task(RunningA2, OperationTaskStatus.Running, "queue-a"), // same host -> de-duplicated
                Task(QueuedB, OperationTaskStatus.Queued, "queue-b"),
                Task(CompletedC, OperationTaskStatus.Completed, "queue-c"), // terminal -> excluded
                Task(RunningUnrouted, OperationTaskStatus.Running, null),  // not routed yet -> excluded
            ],
        });

        // A different operation's routed task must not be targeted.
        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = OtherOperationId,
            TenantId = EryphConstants.DefaultTenantId,
            Status = OperationStatus.Running,
            Tasks = [Task(Guid.NewGuid(), OperationTaskStatus.Running, "queue-other")],
        });
    }

    private static OperationTaskModel Task(Guid id, OperationTaskStatus status, string? routedTo) =>
        new()
        {
            Id = id,
            Status = status,
            RoutedTo = routedTo,
            Progress = [],
        };

    [Fact]
    public async Task FindActiveRouted_returns_only_active_routed_tasks_of_the_operation()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var tasks = await stateStore.For<OperationTaskModel>()
            .ListAsync(new OperationTaskSpecs.FindActiveRouted(OperationId));

        tasks.Select(t => t.Id).Should().BeEquivalentTo([RunningA1, RunningA2, QueuedB]);

        // The dispatcher targets the distinct destinations of those tasks.
        tasks.Select(t => t.RoutedTo).Distinct().Should().BeEquivalentTo(["queue-a", "queue-b"]);
    }
}
