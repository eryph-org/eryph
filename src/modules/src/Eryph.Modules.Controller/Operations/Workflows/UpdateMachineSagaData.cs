using System;
using Eryph.Resources.Machines.Config;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class UpdateMachineSagaData : TaskWorkflowSagaData
    {
        public bool Updated;

        public bool Validated;
        public MachineConfig Config { get; set; }
        public Guid MachineId { get; set; }
        public string AgentName { get; set; }
    }
}