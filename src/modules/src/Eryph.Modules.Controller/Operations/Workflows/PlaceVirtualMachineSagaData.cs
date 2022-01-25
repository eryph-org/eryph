using System;
using Eryph.Resources.Machines.Config;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class PlaceVirtualMachineSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public MachineConfig? Config { get; set; }
    }
}