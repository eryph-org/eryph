using System;
using System.Collections.Generic;
using Haipa.StateDb.Model;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class DestroyMachineSagaData : TaskWorkflowSagaData
    {
        public long MachineId { get; set; }
        
    }
}