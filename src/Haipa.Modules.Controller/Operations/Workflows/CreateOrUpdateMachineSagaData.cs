using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class CreateOrUpdateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }

    }
}