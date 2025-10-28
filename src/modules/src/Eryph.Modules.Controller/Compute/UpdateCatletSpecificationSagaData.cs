using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

public class UpdateCatletSpecificationSagaData
{
    public string? ContentType { get; set; }

    public string? OriginalConfig { get; set; }

    public string? Comment { get; set; }

    public string? AgentName { get; set; }

    public UpdateCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public ISet<Architecture> PendingArchitectures { get; set; } = new HashSet<Architecture>();

    public IReadOnlyDictionary<Architecture, CatletSpecificationVersionVariantSagaData> Variants { get; set; }
        = new Dictionary<Architecture, CatletSpecificationVersionVariantSagaData>();

    public ISet<Architecture> Architectures { get; set; } = new HashSet<Architecture>();
}
