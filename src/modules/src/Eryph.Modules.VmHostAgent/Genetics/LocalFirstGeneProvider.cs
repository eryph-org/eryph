﻿using System;
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
using Eryph.GenePool;
using Eryph.GenePool.Client;
using Eryph.Messages.Genes.Commands;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class LocalFirstGeneProvider(
    IGenePoolFactory genepoolFactory,
    ILogger log,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : IGeneProvider
{
    public EitherAsync<Error, PrepareGeneResponse> ProvideGene(
        GeneType geneType,
        GeneIdentifier geneIdentifier,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        from geneSetInfo in ProvideGeneSet(genePoolPath, geneIdentifier.GeneSet, [], cancel)
        from _1 in guard(geneSetInfo.Id == geneIdentifier.GeneSet,
            Error.New($"The gene '{geneIdentifier}' resolved to the gene set '{geneSetInfo.Id}'. "
                + "This code must only be called with resolved IDs."))
        from geneHash in GetGeneHash(geneSetInfo, geneType, geneIdentifier)
        from _2 in EnsureGene(geneSetInfo, geneIdentifier, geneHash, reportProgress, cancel)
        let localGenePool = genepoolFactory.CreateLocal()
        let timestamp = DateTimeOffset.UtcNow
        from geneSize in localGenePool.GetCachedGeneSize(genePoolPath, geneType, geneIdentifier)
        from validGeneSize in geneSize.ToEitherAsync(
            Error.New($"The gene {geneIdentifier} was not properly extracted."))
        select new PrepareGeneResponse
        {
            RequestedGene = new GeneIdentifierWithType(geneType, geneIdentifier),
            Inventory = new GeneData()
            {
                GeneType = geneType,
                Id = geneIdentifier,
                Hash = geneHash,
                Size = validGeneSize,
            },
            Timestamp = timestamp,
        };

    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier genesetIdentifier,
        CancellationToken cancellationToken) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        from genesetInfo in ProvideGeneSet(genePoolPath, genesetIdentifier, Empty, cancellationToken)
        select genesetInfo.Id;

    private static EitherAsync<Error, string> GetGeneHash(
        GeneSetInfo genesetInfo,
        GeneType geneType,
        GeneIdentifier geneId) =>
        from validHash in GeneSetManifestUtils.FindGeneHash(
                genesetInfo.MetaData, geneType, geneId.GeneName)
            .ToEitherAsync(
            Error.New($"Could not find {geneType.ToString().ToLowerInvariant()} gene {geneId.GeneName} in geneset {genesetInfo.Id}."))
        select validHash;


    private EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        string path,
        GeneSetIdentifier geneSetIdentifier, 
        Seq<GeneSetIdentifier> previousRefs,
        CancellationToken cancel) =>
        from _ in guardnot(previousRefs.Contains(geneSetIdentifier),
            Error.New("Oops, we have disproved Darwin! A circular reference was found in the following gene sequence: "
                + string.Join(" -> ", previousRefs.Add(geneSetIdentifier))))
            .ToEitherAsync()
        let localGenePool = genepoolFactory.CreateLocal()
        from geneSetInfo in localGenePool.ProvideGeneSet(path, geneSetIdentifier, cancel)
            .BiBind(
                Right: i =>
                {
                    if (string.IsNullOrWhiteSpace(i.MetaData.Reference))
                        return i;

                    // We always attempt resolve gene set references remotely as they
                    // can be updated, and we want to use the latest version.
                    log.LogDebug("Geneset {GeneSet} is a reference and will be resolved remotely",
                        geneSetIdentifier);
                    return ProvideGeneSetFromRemote(path, geneSetIdentifier, cancel);
                },
                Left: e =>
                {
                    log.LogDebug(e, "Failed to find geneset {GeneSet} on local gene pool",
                        geneSetIdentifier);

                    return ProvideGeneSetFromRemote(path, geneSetIdentifier, cancel);
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
                    : localGenePool.ProvideGeneSet(path, geneSetIdentifier, cancel)
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
        from cachedGeneSetInfo in localGenePool.CacheGeneSet(path, geneSetInfo, cancel)
        from resolvedGeneSetInfo in Optional(cachedGeneSetInfo.MetaData.Reference)
            .Filter(notEmpty)
            .Match(
                Some: refId =>
                    from validRefId in GeneSetIdentifier.NewEither(refId).ToAsync()
                    from result in ProvideGeneSet(path, validRefId, previousRefs.Add(geneSetIdentifier), cancel)
                    select result,
                None: () => cachedGeneSetInfo)
        select resolvedGeneSetInfo;

    private EitherAsync<Error, GeneSetInfo> ProvideGeneSetFromRemote(string path, GeneSetIdentifier genesetId, CancellationToken cancel)
    {
        return ProvideGeneSetFromRemoteAsync().ToAsync();

        async Task<Either<Error, GeneSetInfo>> ProvideGeneSetFromRemoteAsync()
        {
            log.LogDebug("Trying to find geneset {Geneset} on remote pools", genesetId.Value);
            foreach (var sourceName in genepoolFactory.RemotePools)
            {
                cancel.ThrowIfCancellationRequested();

                var genePool = genepoolFactory.CreateNew(sourceName);
                var result = await genePool.ProvideGeneSet(path, genesetId, cancel);


                var shouldContinue = result.Match(
                    Right: _ =>
                    {
                        log.LogDebug("Found geneset {Geneset} on gene pool {Genepool}",
                            genesetId, sourceName);
                        return false;
                    },
                    Left: e =>
                    {
                        log.LogInformation(e, "Failed to lookup geneset {Geneset} on gene pool {Genepool}.",
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
        GeneSetInfo genesetInfo,
        GeneIdentifier geneId,
        string geneHash,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _  in RightAsync<Error, Unit>(unit)
        let localGenePool = genepoolFactory.CreateLocal()
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
        from cachedGeneInfo in localGenePool.CacheGene(geneInfo, genesetInfo, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to cache gene {GeneId}", geneId);
                return e;
            })
        from ensuredGeneInfo in EnsureGeneParts(cachedGeneInfo, reportProgress, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to ensure parts of gene {GeneId}", geneId);
                return e;
            })
        from mergedGeneInfo in localGenePool.MergeGenes(ensuredGeneInfo, genesetInfo, reportProgress, cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to merge parts of gene {GeneId}", geneId);
                return e;
            })
        select unit;

    private EitherAsync<Error, GeneInfo> EnsureGeneParts(GeneInfo geneInfo, Func<string, int, Task<Unit>> reportProgress, CancellationToken cancel)
    {
        return EnsureGenePartsAsync().ToAsync();

        async Task<Either<Error, GeneInfo>> EnsureGenePartsAsync()
        {

            var localGenePool = genepoolFactory.CreateLocal();
            var parts = (geneInfo.MetaData?.Parts ?? []).ToList();
            var retries = 0;

            var partsMissingLocal = new Arr<string>();
            var sizeAvailableLocal = 0L;

            var stopwatch = Stopwatch.StartNew(); // used for progress message delay
            foreach (var genePart in parts.ToArray())
            {
                cancel.ThrowIfCancellationRequested();

                var res = await localGenePool.RetrieveGenePart(geneInfo, genePart, sizeAvailableLocal,
                    geneInfo.MetaData?.Size ?? 0, reportProgress, stopwatch, cancel);

                res.IfRight(r => { sizeAvailableLocal += r; });

                res.IfLeft(_ => { partsMissingLocal= partsMissingLocal.Add(genePart); });

            }


            while (partsMissingLocal.Count > 0 && retries < 5)
            {
                cancel.ThrowIfCancellationRequested();

                foreach (var genePart in partsMissingLocal.ToArray())
                {
                    cancel.ThrowIfCancellationRequested();

                    var res = await ProvideGenePartFromRemote(geneInfo, genePart, sizeAvailableLocal,
                        geneInfo.MetaData?.Size ?? 0, reportProgress, stopwatch, cancel);

                    res.IfRight(r =>
                    {
                        partsMissingLocal = partsMissingLocal.Remove(genePart);
                        sizeAvailableLocal += r;
                    });
                }

                if (partsMissingLocal.Count <= 0) continue;
                await Task.Delay(2000, cancel);
                retries++;
            }

            if (partsMissingLocal.Count > 0)
            {
                return Error.New($"Failed to provide all part of {geneInfo}.");
            }

            return geneInfo;
        }
    }

    private EitherAsync<Error, GeneInfo> ProvideGeneFromRemote(GeneSetInfo genesetInfo, GeneIdentifier geneIdentifier, string geneHash, CancellationToken cancel)
    {
        return ProvideGeneFromRemoteAsync().ToAsync();

        async Task<Either<Error, GeneInfo>> ProvideGeneFromRemoteAsync()
        {

            foreach (var poolName in genepoolFactory.RemotePools)
            {
                var genePool = genepoolFactory.CreateNew(poolName);
                var result = await genePool.RetrieveGene(genesetInfo, geneIdentifier, geneHash, cancel);

                var shouldContinue = result.Match(
                    Right: _ => false,
                    Left: e =>
                    {
                        log.LogInformation(e, "Failed to retrieve gene {Gene} on pool {Pool}.",
                            geneIdentifier, poolName);
                        return !IsUnexpectedHttpClientError(e);
                    });

                if (!shouldContinue)
                    return result;
            }

            return Error.New($"Could not find gene {geneIdentifier} on any remote pool.");
        }
    }


    private EitherAsync<Error, long> ProvideGenePartFromRemote(
        GeneInfo geneInfo, string genePart, long availableSize, long totalSize, Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch, CancellationToken cancel)
    {
        return ProvideGenePartFromRemoteAsync().ToAsync();

        async Task<Either<Error, long>> ProvideGenePartFromRemoteAsync()
        {
            foreach (var poolName in genepoolFactory.RemotePools)
            {
                var imageSource = genepoolFactory.CreateNew(poolName);
                var result = await imageSource.RetrieveGenePart(geneInfo, genePart, availableSize, totalSize,
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
            .Map(ex => ex is ErrorResponseException
            {
                Response.StatusCode: >= HttpStatusCode.BadRequest
                    and < HttpStatusCode.InternalServerError
                    and not HttpStatusCode.NotFound
            })
            .IfNone(false);
}
