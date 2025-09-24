using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

public class GenePoolReader(IGeneProvider geneProvider) : IGenePoolReader
{
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from content in geneProvider
            .GetGeneContent(uniqueGeneId, geneHash)
            .RunWithCancel(cancellationToken)
            .ToEitherAsync()
        select content;

    public EitherAsync<Error, HashMap<UniqueGeneIdentifier, GeneHash>> GetGenes(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from manifest in geneProvider
            .GetGeneSetManifest(geneSetId)
            .RunWithCancel(cancellationToken)
            .ToEitherAsync()
        from genes in GeneSetTagManifestUtils.GetGenes(manifest)
            .MapLeft(e => Error.New($"The manifest of the gene set '{geneSetId}' is invalid.", e))
            .ToAsync()
        select genes;

    public EitherAsync<Error, Option<GeneSetIdentifier>> GetReferencedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from manifest in geneProvider
            .GetGeneSetManifest(geneSetId)
            .RunWithCancel(cancellationToken)
            .ToEitherAsync()
        from result in Optional(manifest.Reference)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New($"The reference in the gene set '{geneSetId}' is invalid.", e))
            .ToAsync()
        select result;
}
