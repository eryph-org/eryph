using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class UpdateMachineSagaData : TaskWorkflowSagaData
    {
        public bool Updated;

        public bool Validated;
        public CatletConfig? Config { get; set; }
        public Guid MachineId { get; set; }
        public string? AgentName { get; set; }
        public Guid ProjectId { get; set; }
    }
}