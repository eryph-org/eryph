using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class CreateOrUpdateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
    }
}