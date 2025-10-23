using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionConfigModel
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    public required string ConfigYaml { get; set; }

    public required JsonElement ResolvedConfig { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [MaybeNull] public IReadOnlyDictionary<string, string> PinnedGenes { get; set; }
}
