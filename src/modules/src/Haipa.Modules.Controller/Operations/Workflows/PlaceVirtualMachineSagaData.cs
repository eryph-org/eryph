using System;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class PlaceVirtualMachineSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig Config { get; set; }
}
}