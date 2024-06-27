using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Compute
{
    public class UpdateCatletSagaData : TaskWorkflowSagaData
    {
        public bool Updated;

        public bool Validated;
        public CatletConfig? Config { get; set; }

        public CatletConfig? BredConfig { get; set; }

        public Guid CatletId { get; set; }
        public string? AgentName { get; set; }
        public Guid ProjectId { get; set; }
        public Guid TenantId { get; set; }
        public List<string>? PendingGeneNames { get; set; }
        public bool GenesPrepared { get; set; }

    }
}