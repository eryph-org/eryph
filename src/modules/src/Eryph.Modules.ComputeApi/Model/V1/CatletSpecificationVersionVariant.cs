using System.Collections.Generic;
using System.Text.Json;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersionVariant
{
    public required string Architecture { get; set; }

    public required JsonElement BuiltConfig { get; set; }

    /// <summary>
    /// The variable definitions of the built variant, resolved during the spec
    /// build (i.e. bred from the parent chain). Exposed so deployment can collect
    /// variable values without re-resolving the config.
    /// </summary>
    public required IReadOnlyList<CatletVariable> Variables { get; set; }

    public required IReadOnlyList<CatletSpecificationVersionVariantGene>? PinnedGenes { get; set; }
}
