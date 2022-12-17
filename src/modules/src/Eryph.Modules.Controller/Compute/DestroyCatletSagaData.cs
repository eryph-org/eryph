using System;
using System.Collections.Generic;
using Eryph.Modules.Controller.Operations;
using LanguageExt;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyCatletSagaData : TaskWorkflowSagaData
    {
        public Guid MachineId { get; set; }
    }
}