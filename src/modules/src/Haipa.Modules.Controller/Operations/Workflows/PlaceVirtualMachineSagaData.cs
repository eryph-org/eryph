using System;
using Haipa.Resources.Machines.Config;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class PlaceVirtualMachineSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig Config { get; set; }
    }
}