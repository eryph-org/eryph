using System;
using Haipa.Resources.Machines.Config;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class CreateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }

        public CreateVMState State { get; set; }
        public Guid MachineId { get; set; }
    }

    public enum CreateVMState
    {
        Initiated = 0,
        ConfigValidated = 5,
        Placed = 10,
        ImagePrepared = 15,
        Created = 20,
        Updated = 30
    }
}