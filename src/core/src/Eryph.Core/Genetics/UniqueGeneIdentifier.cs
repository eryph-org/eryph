using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

public record UniqueGeneIdentifier(
    GeneType GeneType,
    GeneIdentifier Identifier,
    GeneArchitecture Architecture)
{
    public override string ToString() => $"{GeneType} {Identifier} ({Architecture})";
}
