using Eryph.ConfigModel;

namespace Eryph.Core.Genetics;

public readonly record struct AncestorInfo(GeneSetIdentifier Id, GeneSetIdentifier ResolvedId)
{
    public override string ToString() => Id == ResolvedId
        ? Id.Value
        : $"({Id.Value} -> {ResolvedId.Value})";
}
