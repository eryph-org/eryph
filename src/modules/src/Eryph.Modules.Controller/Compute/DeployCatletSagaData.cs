using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class DeployCatletSagaData : TaskWorkflowSagaData
{
    public DeployCatletSagaState State { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CatletId { get; set; }

    public Guid MetadataId { get; set; }

    public Guid VmId { get; set; }

    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }

    public string ConfigYaml { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }

    public Architecture Architecture { get; set; }
}
