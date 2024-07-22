using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

public record GeneIdentifierWithType(GeneType GeneType, GeneIdentifier GeneIdentifier)
{
    public override string ToString() => $"{GeneType} {GeneIdentifier}";
}
