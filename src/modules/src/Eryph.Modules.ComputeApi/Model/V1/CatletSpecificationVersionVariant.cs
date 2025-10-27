using System.Collections.Generic;
using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersionVariant
{
    public required string Architecture { get; set; }

    public required JsonElement BuiltConfig { get; set; }

    public required IReadOnlyList<CatletSpecificationVersionVariantGene>? PinnedGenes { get; set; }
}
