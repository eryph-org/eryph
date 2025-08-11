using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface IGenePoolReader
{
    /// <summary>
    /// Resolves the given <paramref name="geneSetId"/>. When the given
    /// <paramref name="geneSetId"/> is not reference, the original
    /// <paramref name="geneSetId"/> is returned. The resolved gene set
    /// might be another reference. The caller is responsible for performing
    /// the resolution recursively.
    /// </summary>
    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the manifest of the gene set with the given <paramref name="geneSetId"/>.
    /// </summary>
    public EitherAsync<Error, GenesetTagManifestData> GetGeneSetTagManifest(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the genes of the gene set with the given <paramref name="geneSetId"/>.
    /// </summary>
    public EitherAsync<Error, HashMap<UniqueGeneIdentifier, string>> GetGenes(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the content of the gene with the given <paramref name="uniqueGeneId"/>.
    /// This method will fail for volume genes.
    /// </summary>
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken);
}
