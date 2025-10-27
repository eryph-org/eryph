using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionVariantConfigModel
{
    public Guid Id { get; set; }

    public Guid SpecificationVersionId { get; set; }

    public required string Architecture { get; set; }

    public required JsonElement BuiltConfig { get; set; } 
    
    [MaybeNull] public IReadOnlyDictionary<string, string> PinnedGenes { get; set; }
}
