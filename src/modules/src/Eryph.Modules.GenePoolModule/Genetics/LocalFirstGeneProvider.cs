using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.GenePool;
using Eryph.GenePool.Client;
using Eryph.Messages.Genes.Commands;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class LocalFirstGeneProvider(
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genepoolFactory,
    ILogger log)
    : IGeneProvider
{
    public EitherAsync<Error, PrepareGeneResponse> ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from geneSetInfo in ProvideGeneSet(uniqueGeneId.Id.GeneSet, Empty, localGenePool, cancel)
        from _1 in guard(geneSetInfo.Id == uniqueGeneId.Id.GeneSet,
            Error.New($"The gene '{uniqueGeneId}' resolved to the gene set '{geneSetInfo.Id}'. "
                + "This code must only be called with resolved IDs."))
        from geneHash in GetGeneHash(geneSetInfo, uniqueGeneId)
        from _2 in EnsureGene(genePoolPath, geneSetInfo, uniqueGeneId, geneHash, reportProgress, cancel)
        let timestamp = DateTimeOffset.UtcNow
        from geneSize in localGenePool.GetCachedGeneSize(uniqueGeneId)
        from validGeneSize in geneSize.ToEitherAsync(
            Error.New($"The gene {uniqueGeneId} was not properly extracted."))
        select new PrepareGeneResponse
        {
            RequestedGene = uniqueGeneId,
            Inventory = new GeneData()
            {
                Id = uniqueGeneId,
                Hash = geneHash,
                Size = validGeneSize,
            },
            Timestamp = timestamp,
        };

    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier genesetIdentifier,
        CancellationToken cancellationToken) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from genesetInfo in ProvideGeneSet(genesetIdentifier, Empty, localGenePool, cancellationToken)
        select genesetInfo.Id;

    private static EitherAsync<Error, string> GetGeneHash(
        GeneSetInfo genesetInfo,
        UniqueGeneIdentifier uniqueGeneId) =>
        from validHash in GeneSetTagManifestUtils.FindGeneHash(
                genesetInfo.MetaData,
                uniqueGeneId.GeneType,
                uniqueGeneId.Id.GeneName,
                uniqueGeneId.Architecture)
            .ToEitherAsync(
            Error.New($"Could not find gene {uniqueGeneId} in geneset {genesetInfo.Id}."))
        select validHash;


    private EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        GeneSetIdentifier geneSetIdentifier,
        Seq<GeneSetIdentifier> previousRefs,
        ILocalGenePool localGenePool,
        CancellationToken cancel) =>
        from _ in guardnot(previousRefs.Contains(geneSetIdentifier),
            Error.New("Oops, we have disproved Darwin! A circular reference was found in the following gene sequence: "
                + string.Join(" -> ", previousRefs.Add(geneSetIdentifier))))
            .ToEitherAsync()
        from geneSetInfo in localGenePool.ProvideGeneSet(geneSetIdentifier, cancel)
            .BiBind(
                Right: i =>
                {
                    if (string.IsNullOrWhiteSpace(i.MetaData.Reference))
                        return i;

                    // We always attempt resolve gene set references remotely as they
                    // can be updated, and we want to use the latest version.
                    log.LogDebug("Geneset {GeneSet} is a reference and will be resolved remotely",
                        geneSetIdentifier);
                    return ProvideGeneSetFromRemote(geneSetIdentifier, cancel);
                },
                Left: e =>
                {
                    log.LogDebug(e, "Failed to find geneset {GeneSet} on local gene pool",
                        geneSetIdentifier);

                    return ProvideGeneSetFromRemote(geneSetIdentifier, cancel);
                })
            .BindLeft(remoteError =>
            {
                log.LogDebug(remoteError, "Failed to find geneset {GeneSet} on remote gene pools",
                    geneSetIdentifier);

                // When the error is an HTTP client error (except 404 Not Found), we do not fall back
                // to the local gene pool. Such errors should be presented to the user as they indicate
                // configuration issues like an expired gene pool API key.
                // Otherwise, we try to resolve locally again. This supports scenarios where the gene set
                // was copied directly into the local gene pool. Additionally, it allows us to continue
                // in case the remote pool(s) are not reachable (network issues or 500 errors).
                return IsUnexpectedHttpClientError(remoteError)
                    ? Error.New("Failed to query remote gene pools. Check that any configured API keys are valid.",
                        remoteError)
                    : localGenePool.ProvideGeneSet(geneSetIdentifier, cancel)
                        .MapLeft(localError =>
                        {
                            log.LogInformation(
                                localError,
                                "Failed to find geneset {GeneSet} on any gene pool. The local fallback failed as well.",
                                geneSetIdentifier);
                            return Error.New($"Could not find geneset '{geneSetIdentifier}' on any pool.");
                        });
            })
        // Cache anything received in local store
        from cachedGeneSetInfo in localGenePool.CacheGeneSet(geneSetInfo, cancel)
        from resolvedGeneSetInfo in Optional(cachedGeneSetInfo.MetaData.Reference)
            .Filter(notEmpty)
            .Match(
                Some: refId =>
                    from validRefId in GeneSetIdentifier.NewEither(refId).ToAsync()
                    from result in ProvideGeneSet(validRefId, previousRefs.Add(geneSetIdentifier), localGenePool, cancel)
                    select result,
                None: () => cachedGeneSetInfo)
        select resolvedGeneSetInfo;

    private EitherAsync<Error, GeneSetInfo> ProvideGeneSetFromRemote(GeneSetIdentifier genesetId, CancellationToken cancel)
    {
        return ProvideGeneSetFromRemoteAsync().ToAsync();

        async Task<Either<Error, GeneSetInfo>> ProvideGeneSetFromRemoteAsync()
        {
            log.LogDebug("Trying to find geneset {Geneset} on remote pools", genesetId.Value);
            foreach (var sourceName in genepoolFactory.RemotePools)
            {
                cancel.ThrowIfCancellationRequested();

                var genePool = genepoolFactory.CreateNew(sourceName);
                var result = await genePool.ProvideGeneSet(genesetId, cancel);


                var shouldContinue = result.Match(
                    Right: _ =>
                    {
                        log.LogDebug("Found geneset {Geneset} on gene pool {GenePool}",
                            genesetId, sourceName);
                        return false;
                    },
                    Left: e =>
                    {
                        log.LogInformation(e, "Failed to lookup geneset {Geneset} on gene pool {GenePool}.",
                            genesetId, sourceName);
                        return !IsUnexpectedHttpClientError(e);
                    });

                if (!shouldContinue)
                    return result;
            }

            return Error.New($"Could not find geneset {genesetId} on any pool.");
        }
    }

    private EitherAsync<Error, Unit> EnsureGene(
        string genePoolPath,
        GeneSetInfo genesetInfo,
        UniqueGeneIdentifier geneId,
        string geneHash,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _  in RightAsync<Error, Unit>(unit)
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from geneInfo in localGenePool.RetrieveGene(genesetInfo, geneId, geneHash, cancel)
            .MapLeft(error =>
            {
                log.LogDebug(error, "Failed to find gene {GeneId} on local gene pool", geneId);
                return error;
            })
            .BindLeft(_ => ProvideGeneFromRemote(genesetInfo, geneId, geneHash, cancel))
            .MapLeft(error =>
            {
                log.LogDebug(error, "Failed to find gene {GeneId} on remote gene pools", geneId);
                return IsUnexpectedHttpClientError(error)
                    ? Error.New("Failed to query remote gene pools. Check that any configured API keys are valid.",
                        error)
                    : error;
            })
        from cachedGeneInfo in localGenePool.CacheGene(geneInfo, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to cache gene {GeneId}", geneId);
                return e;
            })
        from ensuredGeneInfo in EnsureGeneParts(genePoolPath, cachedGeneInfo, reportProgress, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to ensure parts of gene {GeneId}", geneId);
                return e;
            })
        from mergedGeneInfo in localGenePool.MergeGeneParts(ensuredGeneInfo, reportProgress, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to merge parts of gene {GeneId}", geneId);
                return e;
            })
        select unit;

    private EitherAsync<Error, GeneInfo> EnsureGeneParts(
        string genePoolPath,
        GeneInfo geneInfo,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _ in RightAsync<Error, Unit>(unit)
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        let totalBytes = geneInfo.MetaData?.Size ?? 0
        // When the gene has already been fetched and extracted, geneInfo.Metadata
        // will just be null. In this case, the following code is mostly skipped
        // as there are no gene parts to ensure.
        from genePartsWithPaths in (geneInfo.MetaData?.Parts).ToSeq()
            .Map(part => from path in localGenePool.GetGenePartPath(geneInfo.Id, geneInfo.Hash, part)
                         select (Part: part, Path: path))
            .SequenceSerial()
        let stopwatch = Stopwatch.StartNew()
        from localResult in TryAsync(async () =>
        {
            var availableBytes = 0L;
            var missingParts = new Arr<(string Part, string Path)>();
            foreach (var partWithPath in genePartsWithPaths)
            {
                cancel.ThrowIfCancellationRequested();

                var res = await localGenePool.RetrieveGenePart(geneInfo, partWithPath.Part,
                    partWithPath.Path,
                    availableBytes,
                    totalBytes, reportProgress, stopwatch, cancel);

                res.IfRight(r => { availableBytes += r; });

                res.IfLeft(_ => { missingParts = missingParts.Add(partWithPath); });
            }

            return (AvailableBytes: availableBytes, MissingParts: missingParts);
        }).ToEither()
        from __ in TryAsync(async () =>
        {
            var retries = 0;
            var (availableBytes, missingParts) = localResult;

            while (missingParts.Count > 0 && retries < 5)
            {
                cancel.ThrowIfCancellationRequested();

                foreach (var genePart in missingParts)
                {
                    cancel.ThrowIfCancellationRequested();

                    var res = await ProvideGenePartFromRemote(geneInfo, genePart.Part, genePart.Path,
                        availableBytes, totalBytes, reportProgress, stopwatch, cancel);

                    res.IfRight(r =>
                    {
                        missingParts = missingParts.Remove(genePart);
                        availableBytes += r;
                    });
                }

                if (missingParts.Count == 0)
                    return unit;

                await Task.Delay(2000, cancel);
                retries++;
            }

            return missingParts.Count > 0
                ? Error.New($"Failed to provide all part of {geneInfo}.").Throw()
                : unit;
        }).ToEither()
        select geneInfo;

    private EitherAsync<Error, GeneInfo> ProvideGeneFromRemote(
        GeneSetInfo genesetInfo,
        UniqueGeneIdentifier uniqueGeneId,
        string geneHash,
        CancellationToken cancel)
    {
        return ProvideGeneFromRemoteAsync().ToAsync();

        async Task<Either<Error, GeneInfo>> ProvideGeneFromRemoteAsync()
        {

            foreach (var poolName in genepoolFactory.RemotePools)
            {
                var genePool = genepoolFactory.CreateNew(poolName);
                var result = await genePool.RetrieveGene(genesetInfo, uniqueGeneId, geneHash, cancel);

                var shouldContinue = result.Match(
                    Right: _ => false,
                    Left: e =>
                    {
                        log.LogInformation(e, "Failed to retrieve gene {Gene} on pool {Pool}.",
                            uniqueGeneId, poolName);
                        return !IsUnexpectedHttpClientError(e);
                    });

                if (!shouldContinue)
                    return result;
            }

            return Error.New($"Could not find gene {uniqueGeneId} on any remote pool.");
        }
    }


    private EitherAsync<Error, long> ProvideGenePartFromRemote(
        GeneInfo geneInfo,
        string genePart,
        string genePartPath,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel)
    {
        return ProvideGenePartFromRemoteAsync().ToAsync();

        async Task<Either<Error, long>> ProvideGenePartFromRemoteAsync()
        {
            foreach (var poolName in genepoolFactory.RemotePools)
            {
                var imageSource = genepoolFactory.CreateNew(poolName);
                var result = await imageSource.RetrieveGenePart(geneInfo, genePart, genePartPath, availableSize, totalSize,
                    reportProgress, stopwatch, cancel);

                var shouldContinue = result.Match(
                    Right: _ => false,
                    Left: error =>
                    {
                        log.LogInformation(
                            error,
                            "Failed to retrieve gene part {GenePart} of gene {Gene} on source {Source}.",
                            genePart, geneInfo, poolName);
                        return !IsUnexpectedHttpClientError(error);
                    });

                if (!shouldContinue)
                    return result;
            }

            return Error.New($"Could not find gene part {genePart} of gene {geneInfo} on any remote source.");
        }
    }

    private static bool IsUnexpectedHttpClientError(Error error) =>
        error.Exception
            .Map(ex => ex is GenepoolClientException
            {
                StatusCode: >= HttpStatusCode.BadRequest
                    and < HttpStatusCode.InternalServerError
                    and not HttpStatusCode.NotFound
            })
            .IfNone(false);
}
