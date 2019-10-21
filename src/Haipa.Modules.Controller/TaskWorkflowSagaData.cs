using System;
using Haipa.VmConfig;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    public class TaskWorkflowSagaData : ISagaData
    {
        // these two are required by Rebus
        public Guid Id { get; set; }
        public int Revision { get; set; }

        public Guid OperationId { get; set; }
        public Guid InitiatingTaskId { get; set; }

    }
}