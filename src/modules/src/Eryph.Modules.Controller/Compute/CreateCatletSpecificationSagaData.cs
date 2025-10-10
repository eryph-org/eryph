using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.Compute;

public class CreateCatletSpecificationSagaData : TaskWorkflowSagaData
{
    public string? ConfigYaml { get; set; }

    public string? Comment { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public string? Name { get; set; }

    public string? AgentName { get; set; }

    public CreateCatletSpecificationSagaState State { get; set; }

    public Guid ProjectId { get; set; }

    public Guid SpecificationId { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
