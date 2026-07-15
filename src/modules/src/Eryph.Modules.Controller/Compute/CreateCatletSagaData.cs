using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class CreateCatletSagaData : TaskWorkflowSagaData
{
    public string? ContentType { get; set; }

    public string? OriginalConfig { get; set; }

    public CatletConfig? BuiltConfig { get; set; }

    public string? AgentName { get; set; }

    public CreateCatletSagaState State { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>
    /// The site realizing the catlet's environment, resolved once when the saga starts. It is both
    /// the site placement stays within and the site the catlet is pinned to.
    /// </summary>
    public Guid SiteId { get; set; }

    public ProjectName? ProjectName { get; set; }

    public Architecture? Architecture { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; } =
        new Dictionary<UniqueGeneIdentifier, GeneHash>();
}
