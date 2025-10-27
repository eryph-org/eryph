using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.Compute;

public class CreateCatletSpecificationSagaData : TaskWorkflowSagaData
{
    public string? ContentType { get; set; }

    public string? Configuration { get; set; }

    public string? Comment { get; set; }

    public string? AgentName { get; set; }

    public CreateCatletSpecificationSagaState State { get; set; }

    public Guid ProjectId { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public ISet<Architecture> PendingArchitectures = new HashSet<Architecture>();

    public IReadOnlyDictionary<Architecture, CatletSpecificationVersionVariantSagaData> Variants { get; set; }
        = new Dictionary<Architecture, CatletSpecificationVersionVariantSagaData>();

    public ISet<Architecture> Architectures = new HashSet<Architecture>();
}
