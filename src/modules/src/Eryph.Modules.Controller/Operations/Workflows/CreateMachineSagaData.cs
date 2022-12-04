using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class CreateMachineSagaData : TaskWorkflowSagaData
    {
        public CatletConfig? Config { get; set; }
        public string? AgentName { get; set; }

        public CreateVMState State { get; set; }
        public Guid MachineId { get; set; }
    }
}