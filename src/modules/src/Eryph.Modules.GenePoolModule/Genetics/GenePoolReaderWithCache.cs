using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class GenePoolReaderWithCache(
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genePoolFactory,
    IRepositoryGenePoolReader repositoryGenePoolReader,
    ILogger logger)
    : IGenePoolReader
{
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        from cachedGeneContent in localGenePool.GetCachedGeneContent(uniqueGeneId, cancellationToken)
        from result in cachedGeneContent.ToEitherAsync(
            Error.New($"The gene {uniqueGeneId} is not available in local gene pool."))
        // TODO Implement download
        select result;

    public EitherAsync<Error, GenesetTagManifestData> GetGeneSetTagManifest(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        from cachedGeneSet in localGenePool.GetCachedGeneSet(geneSetId, cancellationToken)
        from pulledGeneSet in cachedGeneSet
            .Filter(gsi => string.IsNullOrWhiteSpace(gsi.MetaData.Reference))
            .Match(
                Some: g => RightAsync<Error, Option<GeneSetInfo>>(Some(g)),
                None: () => PullAndCacheGeneSet(geneSetId, localGenePool, cancellationToken))
        from result in (pulledGeneSet | cachedGeneSet).ToEitherAsync(
            Error.New($"The gene set {geneSetId} is not available in local or remote gene pools."))
        select result.MetaData;

    private EitherAsync<Error, Option<GeneSetInfo>> PullAndCacheGeneSet(
        GeneSetIdentifier geneSetId,
        ILocalGenePool localGenePool,
        CancellationToken cancellationToken) =>
        from pulledGeneSet in repositoryGenePoolReader.ProvideGeneSet(geneSetId, cancellationToken)
        from _ in pulledGeneSet
            .Map(gsi => localGenePool.CacheGeneSet(gsi, cancellationToken))
            .Sequence()
        select pulledGeneSet;

    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from manifest in GetGeneSetTagManifest(geneSetId, cancellationToken)
        from result in Optional(manifest.Reference)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New($"The reference in the gene set '{geneSetId}' is invalid.", e))
            .ToAsync()
        select result.IfNone(geneSetId);
}
