using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersion
{
    public required string Id { get; set; }

    public required string SpecificationId { get; set; }

    public string? Comment { get; set; }

    public required string Configuration { get; set; }

    public required JsonElement ResolvedConfig { get; set; }

    public required IReadOnlyList<CatletSpecificationVersionGene>? Genes { get; set; }
}
