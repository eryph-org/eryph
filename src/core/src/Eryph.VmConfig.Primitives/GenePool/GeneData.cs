using Eryph.Core.Genetics;

namespace Eryph.GenePool;

public class GeneData
{
    public required UniqueGeneIdentifier Id { get; set; }

    public required long Size { get; set; }

    public required GeneHash Hash { get; set; }
}
