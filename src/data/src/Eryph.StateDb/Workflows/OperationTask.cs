using System;
using Dbosoft.Rebus.Operations;
using Eryph.StateDb.Model;
using OperationTaskStatus = Dbosoft.Rebus.Operations.OperationTaskStatus;

namespace Eryph.StateDb.Workflows;

internal class OperationTask : IOperationTask
{
    public OperationTask(OperationTaskModel model)
    {
        Model = model;
    }

    public OperationTaskModel Model { get; }

    public Guid Id => Model.Id;
    public Guid OperationId => Model.Operation.Id;
    public Guid InitiatingTaskId => Model.ParentTaskId;

    public OperationTaskStatus Status
    {
        get
        {
            return Model.Status switch
            {
                Eryph.StateDb.Model.OperationTaskStatus.Queued => OperationTaskStatus.Queued,
                Eryph.StateDb.Model.OperationTaskStatus.Running => OperationTaskStatus.Running,
                Eryph.StateDb.Model.OperationTaskStatus.Failed => OperationTaskStatus.Failed,
                Eryph.StateDb.Model.OperationTaskStatus.Completed => OperationTaskStatus.Completed,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}