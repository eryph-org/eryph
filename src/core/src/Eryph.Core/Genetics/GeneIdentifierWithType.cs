using System.Diagnostics.CodeAnalysis;
using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

public record GeneIdentifierWithType
{
    [SetsRequiredMembers]
    public GeneIdentifierWithType(GeneType geneType, GeneIdentifier geneIdentifier)
    {
        GeneType = geneType;
        GeneIdentifier = geneIdentifier;
    }

    public GeneIdentifierWithType() { }

    public required GeneType GeneType { get; init; }

    public required GeneIdentifier GeneIdentifier { get; init; }
}
