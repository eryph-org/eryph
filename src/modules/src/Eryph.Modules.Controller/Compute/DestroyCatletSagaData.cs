using System;
using Dbosoft.Rebus.Operations.Workflow;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyCatletSagaData : TaskWorkflowSagaData
    {
        public Guid MachineId { get; set; }
    }
}