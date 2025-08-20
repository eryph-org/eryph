using Eryph.Core.Genetics;
using Eryph.GenePool.Model;

namespace Eryph.Modules.GenePool.Genetics;

public static class GeneHashExtensions
{
    /// <summary>
    /// Converts a <see cref="GeneHash"/> to a <see cref="Gene"/> which can be
    /// used to make requests to the gene pool.
    /// </summary>
    /// <remarks>
    /// By convention, the ID of a gene in the gene pool is its hash.
    /// </remarks>
    public static Gene ToGene(this GeneHash geneHash) => Gene.New(geneHash.Hash);
}
