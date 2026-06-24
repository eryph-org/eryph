using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersion
{
    public required string Id { get; set; }

    public required string SpecificationId { get; set; }

    public string? Comment { get; set; }

    public required CatletSpecificationConfig Configuration { get; set; }

    public required IReadOnlyList<CatletSpecificationVersionVariant>? Variants { get; set; }
}
