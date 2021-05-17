using System;
using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class UpdateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
        public long MachineId { get; set; }
        public string AgentName { get; set; }

        public bool Validated;
        public bool Updated;
    }


}