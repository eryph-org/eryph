using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Compute
{
    public class CreateCatletSagaData : TaskWorkflowSagaData
    {
        public CatletConfig? Config { get; set; }
        public string? AgentName { get; set; }

        public CreateVMState State { get; set; }
        public Guid MachineId { get; set; }
        public Guid TenantId { get; set; }
    }
}