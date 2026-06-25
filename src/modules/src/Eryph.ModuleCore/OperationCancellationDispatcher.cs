using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Rebus.Bus;

namespace Eryph.ModuleCore;

/// <summary>
/// Requests cancellation of a running operation by delivering the request to the hosts
/// that are running its tasks (using the destination recorded when each task was
/// dispatched). Hosts trip the operation's cancellation tokens; a cancellation for a
/// finished or unknown operation has no active tasks to target and is a no-op.
/// </summary>
public interface IOperationCancellationDispatcher
{
    Task CancelOperation(Guid operationId);
}

public class OperationCancellationDispatcher(IBus bus, IStateStore stateStore)
    : IOperationCancellationDispatcher
{
    public async Task CancelOperation(Guid operationId)
    {
        var tasks = await stateStore.For<OperationTaskModel>()
            .ListAsync(new OperationTaskSpecs.FindActiveRouted(operationId));

        var message = new OperationCancellationRequestedEvent { OperationId = operationId };
        foreach (var destination in tasks.Select(t => t.RoutedTo).OfType<string>().Distinct())
            await bus.Advanced.Routing.Send(destination, message);
    }
}
