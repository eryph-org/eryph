using Eryph.Serializers;
using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class CatletSpecificationVersion
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    public required string ConfigYaml { get; set; }

    // TODO use a wrapper property with lazy serialization?

    public required string ResolvedConfig { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public IList<CatletSpecificationVersionGene> Genes { get; set; } = null!;
}
