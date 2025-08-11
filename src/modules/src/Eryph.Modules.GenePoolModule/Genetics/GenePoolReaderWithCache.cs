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
    : IGenePoolReader, ILocalGenePoolReader
{
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        from cachedGeneContent in localGenePool.GetCachedGeneContent(uniqueGeneId, cancellationToken)
        from pulledGene in cachedGeneContent.Match(
            Some: c => RightAsync<Error, Option<string>>(c),
            None: () => PullAndCacheGene(uniqueGeneId, localGenePool, cancellationToken))
        from result in pulledGene.ToEitherAsync(
            Error.New($"The gene {uniqueGeneId} is not available in local or remote gene pools."))
        select result;

    private EitherAsync<Error, Option<string>> PullAndCacheGene(
        UniqueGeneIdentifier uniqueGeneId,
        ILocalGenePool localGenePool,
        CancellationToken cancellationToken) =>
        // We need the gene set manifest to get the gene hash which
        // is required by the gene pool API. In almost all cases
        // the gene set manifest should already be present in the local cache.
        from geneSetManifest in GetGeneSetTagManifest(uniqueGeneId.Id.GeneSet, cancellationToken)
        from genes in GeneSetTagManifestUtils.GetGenes(geneSetManifest)
            .MapLeft(e => Error.New($"The manifest of the gene set '{uniqueGeneId.Id.GeneSet}' is invalid.", e))
            .ToAsync()
        from geneHash in genes.Find(uniqueGeneId)
            .ToEitherAsync(Error.New($"The gene {uniqueGeneId} is not part of the gene set."))
        from pulledGene in repositoryGenePoolReader.ProvideGeneContent(uniqueGeneId, geneHash, cancellationToken)
        from content in pulledGene
            .Map(g => localGenePool.CacheGeneContent(g, cancellationToken))
            .Sequence()
        select content;

    public EitherAsync<Error, GenesetTagManifestData> GetGeneSetTagManifest(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        from cachedGeneSet in localGenePool.GetCachedGeneSet(geneSetId, cancellationToken)
        from pulledGeneSet in cachedGeneSet
            // gene set references should always be checked online
            .Filter(gsi => string.IsNullOrWhiteSpace(gsi.Manifest.Reference))
            .Match(
                Some: g => RightAsync<Error, Option<GeneSetInfo>>(Some(g)),
                None: () => PullAndCacheGeneSet(geneSetId, localGenePool, cancellationToken))
        from result in (pulledGeneSet | cachedGeneSet).ToEitherAsync(
            Error.New($"The gene set {geneSetId} is not available in local or remote gene pools."))
        select result.Manifest;

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

    // TODO remove these (we added these just for refactoring)

    public EitherAsync<Error, Option<GeneSetIdentifier>> GetGenesetReference(
        GeneSetIdentifier geneSetId) =>
        from resolvedGeneSetId in ResolveGeneSet(geneSetId, CancellationToken.None)
        let result = Some(resolvedGeneSetId).Filter(id => id != geneSetId)
        select result;

    public EitherAsync<Error, string> ReadGeneContent(
        UniqueGeneIdentifier uniqueGeneId) =>
        GetGeneContent(uniqueGeneId, CancellationToken.None);

    public EitherAsync<Error, HashMap<UniqueGeneIdentifier, string>> GetGenes(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from manifest in GetGeneSetTagManifest(geneSetId, cancellationToken)
        from genes in GeneSetTagManifestUtils.GetGenes(manifest)
            .MapLeft(e => Error.New($"The manifest of the gene set '{geneSetId}' is invalid.", e))
            .ToAsync()
        select genes.ToSeq().Map(kv => (kv.Key, kv.Value.Value)).ToHashMap();
}
