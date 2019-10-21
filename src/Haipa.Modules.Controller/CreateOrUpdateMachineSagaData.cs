using Haipa.VmConfig;

namespace Haipa.Modules.Controller
{
    public class CreateOrUpdateMachineSagaData : TaskWorkflowSagaData
    {
        public MachineConfig Config { get; set; }

    }
}