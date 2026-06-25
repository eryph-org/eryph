using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Eryph.Rebus;
using Rebus.Bus;

namespace Eryph.ModuleCore;

/// <summary>
/// Requests cancellation of a running operation by broadcasting to every host that
/// runs operation tasks. Hosts that are running one of the operation's tasks trip its
/// cancellation token; everyone else ignores it, so cancelling a finished or unknown
/// operation is a harmless no-op.
/// </summary>
public interface IOperationCancellationDispatcher
{
    Task CancelOperation(Guid operationId);
}

public class OperationCancellationDispatcher(IBus bus) : IOperationCancellationDispatcher
{
    public Task CancelOperation(Guid operationId) =>
        bus.Advanced.Topics.Publish(
            QueueNames.OperationCancellation,
            new OperationCancellationRequestedEvent { OperationId = operationId });
}
