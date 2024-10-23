using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

/// <summary>
/// This record uniquely identifies a gene as it also
/// specifies the gene's architecture.
/// </summary>
public record UniqueGeneIdentifier(
    GeneType GeneType,
    GeneIdentifier Id,
    Architecture Architecture)
{
    public override string ToString() => $"{GeneType.ToString().ToLowerInvariant()} {Id} ({Architecture})";
}
