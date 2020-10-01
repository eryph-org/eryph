using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class CreateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }
    }
}