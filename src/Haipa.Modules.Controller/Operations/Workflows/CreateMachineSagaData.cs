using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class CreateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
        public string AgentName { get; set; }
        public VirtualMachineMetadata Metadata { get; set; }
    }
}