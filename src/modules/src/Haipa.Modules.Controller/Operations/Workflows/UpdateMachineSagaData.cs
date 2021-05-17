using System;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

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