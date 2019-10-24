using System;
using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class PlaceVirtualMachineSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig Config { get; set; }
}
}