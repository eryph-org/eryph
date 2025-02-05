using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class PrepareCatletConfigSagaData : TaskWorkflowSagaData
{
    public PrepareCatletConfigState State { get; set; }

    public CatletConfig? Config { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier>? ResolvedGenes { get; set; }

    public Guid CatletId { get; set; }

    public string? AgentName { get; set; }

    public Guid ProjectId { get; set; }

    public Guid TenantId { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> PendingGenes { get; set; } = [];
}