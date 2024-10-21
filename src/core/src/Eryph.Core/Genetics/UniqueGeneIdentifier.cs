using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

public record UniqueGeneIdentifier(
    GeneType GeneType,
    GeneIdentifier Id,
    Architecture Architecture)
{
    public override string ToString() => $"{GeneType.ToString().ToLowerInvariant()} {Id} ({Architecture})";
}
