using System;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyVirtualDiskSagaData : TaskWorkflowSagaData
    {
        public Guid DiskId { get; set; }
    }
}