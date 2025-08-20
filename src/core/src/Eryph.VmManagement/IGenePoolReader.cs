using System.Threading;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface IGenePoolReader
{
    /// <summary>
    /// Returns the ID of the gene set which is referenced by the gene set
    /// with the given <paramref name="geneSetId"/>. When the given
    /// <paramref name="geneSetId"/> is not reference, <see cref="OptionNone"/>
    /// is returned. When the gene set cannot be found, an <see cref="Error"/>
    /// is returned.
    /// </summary>
    /// <remarks>
    /// The returned gene set might itself be another reference. The caller is
    /// responsible for performing the resolution recursively.
    /// </remarks>
    public EitherAsync<Error, Option<GeneSetIdentifier>> GetReferencedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the genes of the gene set with the given <paramref name="geneSetId"/>.
    /// </summary>
    public EitherAsync<Error, HashMap<UniqueGeneIdentifier, GeneHash>> GetGenes(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the content of the gene with the given <paramref name="uniqueGeneId"/>.
    /// This method will fail for volume genes.
    /// </summary>
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken);
}
