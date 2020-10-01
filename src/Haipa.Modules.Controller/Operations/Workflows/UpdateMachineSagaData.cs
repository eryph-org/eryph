using System;
using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class UpdateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }

    }
}