using System;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class DestroyMachineSagaData : TaskWorkflowSagaData
    {
        public Guid MachineId { get; set; }
    }
}