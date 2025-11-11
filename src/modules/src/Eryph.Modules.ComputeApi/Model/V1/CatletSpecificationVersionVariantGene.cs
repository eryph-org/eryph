using Eryph.Core.Genetics;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersionVariantGene
{
    public required GeneType GeneType { get; set; }

    public required string GeneSet { get; set; }

    public required string Name { get; set; }

    public required string Architecture { get; set; }

    public required string Hash { get; set; }
}
