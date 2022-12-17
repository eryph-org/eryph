using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Compute
{
    public class CreateCatletSagaData : TaskWorkflowSagaData
    {
        public CatletConfig? Config { get; set; }
        public string? AgentName { get; set; }

        public CreateVMState State { get; set; }
        public Guid MachineId { get; set; }
    }
}