using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSpecificationSagaData
{
    public string? ContentType { get; set; }

    public string? ConfigYaml { get; set; }

    public string? Comment { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public string? Name { get; set; }

    public string? AgentName { get; set; }

    public UpdateCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } = new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
