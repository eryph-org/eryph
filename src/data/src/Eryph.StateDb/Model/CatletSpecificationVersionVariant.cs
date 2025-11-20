using System;
using System.Collections.Generic;
using Eryph.Core.Genetics;

namespace Eryph.StateDb.Model;

public class CatletSpecificationVersionVariant
{
    public required Guid Id { get; set; }

    public required Guid SpecificationVersionId { get; set; }

    public required Architecture Architecture { get; set; }

    public required string BuiltConfig { get; set; }

    public IList<CatletSpecificationVersionVariantGene> PinnedGenes { get; set; } = null!;
}
