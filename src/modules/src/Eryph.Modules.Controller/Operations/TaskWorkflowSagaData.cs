using System;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations
{
    public class TaskWorkflowSagaData : ISagaData
    {
        public Guid OperationId { get; set; }

        public Guid InitiatingTaskId { get; set; }

        // these two are required by Rebus
        public Guid Id { get; set; }
        public int Revision { get; set; }
    }
}