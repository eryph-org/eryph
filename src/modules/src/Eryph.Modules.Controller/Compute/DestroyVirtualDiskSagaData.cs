using System;
using Dbosoft.Rebus.Operations.Workflow;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyVirtualDiskSagaData : TaskWorkflowSagaData
    {
        public Guid DiskId { get; set; }
    }
}