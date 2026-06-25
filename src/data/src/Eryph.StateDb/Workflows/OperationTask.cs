using System;
using Dbosoft.Rebus.Operations;
using Eryph.StateDb.Model;
using OperationTaskStatus = Dbosoft.Rebus.Operations.OperationTaskStatus;

namespace Eryph.StateDb.Workflows;

internal class OperationTask(OperationTaskModel model) : IOperationTask
{
    public OperationTaskModel Model { get; } = model;

    public Guid Id => Model.Id;
    public Guid OperationId => Model.Operation.Id;
    public Guid InitiatingTaskId => Model.ParentTaskId;

    public OperationTaskStatus Status
    {
        get
        {
            return Model.Status switch
            {
                StateDb.Model.OperationTaskStatus.Queued => OperationTaskStatus.Queued,
                StateDb.Model.OperationTaskStatus.Running => OperationTaskStatus.Running,
                StateDb.Model.OperationTaskStatus.Failed => OperationTaskStatus.Failed,
                StateDb.Model.OperationTaskStatus.Completed => OperationTaskStatus.Completed,
                StateDb.Model.OperationTaskStatus.Cancelled => OperationTaskStatus.Cancelled,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }
}
