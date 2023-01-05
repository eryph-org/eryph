using System;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyCatletSagaData : TaskWorkflowSagaData
    {
        public Guid MachineId { get; set; }
    }
}