using System;
using System.Collections.Generic;
using Haipa.StateDb.Model;
using Haipa.VmConfig;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class DestroyMachineSagaData : TaskWorkflowSagaData
    {
        public long MachineId { get; set; }
        
    }
}