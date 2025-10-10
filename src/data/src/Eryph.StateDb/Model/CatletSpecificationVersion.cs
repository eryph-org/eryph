using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class CatletSpecificationVersion
{
    public Guid Id { get; set; }

    public Guid SpecificationId { get; set; }

    /// <summary>
    /// The ID of the catlet when this specification has been deployed.
    /// </summary>
    public Guid? CatletId { get; set; }

    public required string ConfigYaml { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public IList<CatletSpecificationVersionGene> Genes { get; set; } = null!;
}
