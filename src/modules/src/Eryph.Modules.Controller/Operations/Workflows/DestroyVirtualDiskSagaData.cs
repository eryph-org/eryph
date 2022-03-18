using System;
using System.Collections.Generic;
using LanguageExt;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class DestroyVirtualDiskSagaData : TaskWorkflowSagaData
    {
        public Guid DiskId { get; set; }
    }
}