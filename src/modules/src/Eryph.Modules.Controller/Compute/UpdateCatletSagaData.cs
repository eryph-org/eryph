using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Compute
{
    public class UpdateCatletSagaData : TaskWorkflowSagaData
    {
        public bool Updated;

        public bool Validated;
        public CatletConfig? Config { get; set; }
        public Guid CatletId { get; set; }
        public string? AgentName { get; set; }
        public Guid ProjectId { get; set; }
    }
}