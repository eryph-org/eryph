using System;
using System.Collections.Generic;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class DeployCatletSpecificationSagaData
{
    public DeployCatletSpecificationSagaState State { get; set; }

    public string? AgentName { get; set; }

    public Architecture? Architecture { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>The environment this deployment targets. It belongs to the deployment, not to the
    /// specification, which can be deployed into several environments.</summary>
    public EnvironmentName? Environment { get; set; }

    /// <summary>The site realizing the deployment's environment, resolved when the catlet was placed.</summary>
    public Guid SiteId { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public string? ContentType { get; set; }

    public string? Configuration { get; set; }

    public bool Redeploy { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } =
        new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
