using System;
using Dbosoft.Rebus.Operations;
using Eryph.StateDb.Model;
using OperationStatus = Eryph.StateDb.Model.OperationStatus;

namespace Eryph.StateDb.Workflows
{
    public class Operation : IOperation
    {
        public Operation(OperationModel model)
        {
            Model = model;
        }

        public OperationModel Model { get; }


        public Guid Id => Model.Id;

        public Dbosoft.Rebus.Operations.OperationStatus Status
        {
            get
            {
                return Model.Status switch
                {
                    OperationStatus.Queued => Dbosoft.Rebus.Operations.OperationStatus.Queued,
                    OperationStatus.Running => Dbosoft.Rebus.Operations.OperationStatus.Running,
                    OperationStatus.Failed => Dbosoft.Rebus.Operations.OperationStatus.Failed,
                    OperationStatus.Completed => Dbosoft.Rebus.Operations.OperationStatus.Completed,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }
}