using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class OperationTaskSpecs
{
    /// <summary>
    /// Finds the still-active (queued or running) tasks of an operation that have
    /// been routed to a host, so a cancellation request can be delivered directly
    /// to the hosts running them.
    /// </summary>
    public sealed class FindActiveRouted : Specification<OperationTaskModel>
    {
        public FindActiveRouted(Guid operationId)
        {
            Query.Where(x => x.OperationId == operationId
                && (x.Status == OperationTaskStatus.Queued || x.Status == OperationTaskStatus.Running)
                && x.RoutedTo != null);
        }
    }
}
