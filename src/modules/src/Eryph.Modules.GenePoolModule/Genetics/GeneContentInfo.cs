using Eryph.Core.Genetics;

namespace Eryph.Modules.GenePool.Genetics;

public record GeneContentInfo(
    UniqueGeneIdentifier UniqueId,
    GeneHash Hash,
    byte[] Content,
    int Size,
    string Format);
