using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class DeployCatletSpecificationSagaData : TaskWorkflowSagaData
{
    public DeployCatletSpecificationSagaState State { get; set; }

    public string? AgentName { get; set; }

    public Architecture? Architecture { get; set; }

    public Guid ProjectId { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public bool Redeploy { get; set; }

    public string ContentType { get; set; }

    public string? ConfigYaml { get; set; }

    public IReadOnlyDictionary<string, string> Variables { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
