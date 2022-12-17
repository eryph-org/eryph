using System;
using System.Collections.Generic;
using Eryph.Modules.Controller.Operations;
using LanguageExt;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyVirtualDiskSagaData : TaskWorkflowSagaData
    {
        public Guid DiskId { get; set; }
    }
}