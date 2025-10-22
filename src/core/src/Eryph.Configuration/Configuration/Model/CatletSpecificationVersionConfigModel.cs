using System;
using System.Diagnostics.CodeAnalysis;

namespace Eryph.Configuration.Model;

public class CatletSpecificationVersionConfigModel
{
    public Guid SpecificationId { get; set; }

    public required string ConfigYaml { get; set; }

    public required string ResolvedConfig { get; set; }

    [MaybeNull] public string Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
