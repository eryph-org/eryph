using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.GenePool;
using Eryph.GenePool.Client;
using Eryph.Messages.Genes.Commands;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using Microsoft.Identity.Client;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class LocalFirstGeneProvider(
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genepoolFactory,
    ILogger log)
    : IGeneProvider
{
    public Aff<CancelRt, GeneSetInfo> GetGeneSetManifest(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from cachedGeneSet in localGenePool.GetCachedGeneSet(geneSetId)
        from pulledGeneSet in cachedGeneSet
            // gene set references should always be checked online
            .Filter(gsi => string.IsNullOrWhiteSpace(gsi.Manifest.Reference))
            .Match(
                Some: g => SuccessAff<CancelRt, Option<GeneSetInfo>>(Some(g)),
                None: () => PullAndCacheGeneSet(geneSetId))
        from result in (pulledGeneSet | cachedGeneSet).ToAff(
            Error.New($"The gene set {geneSetId} is not available in local or remote gene pools."))
        select result;


    private Aff<CancelRt, Option<GeneSetInfo>> PullAndCacheGeneSet(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from pulledGeneSet in IterateGenePools<GeneSetInfo>(
            genepoolFactory,
            pool => pool.GetGeneSet(geneSetId))
        from _ in pulledGeneSet
            .Map(gsi => localGenePool.CacheGeneSet(gsi, CancellationToken.None).ToAff())
            .Sequence()
        select pulledGeneSet;

    public Aff<CancelRt, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from cachedGeneContent in localGenePool.GetCachedGeneContent(uniqueGeneId)
        from pulledGeneContent in cachedGeneContent.Match(
            Some: c => SuccessAff<Option<string>>(c),
            None: () => PullAndCacheGene(uniqueGeneId, geneHash))
        from result in pulledGeneContent.ToAff(
            Error.New($"The gene {uniqueGeneId} ({geneHash}) is not available in local or remote gene pools."))
        select result;

    private Aff<CancelRt, Option<string>> PullAndCacheGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from pulledGene in IterateGenePools(
            genepoolFactory,
            genePool => genePool.GetGeneContent(uniqueGeneId, geneHash))
        from content in pulledGene
            .Map(g => localGenePool.CacheGeneContent(g, CancellationToken.None).ToAff())
            .Sequence()
        select content;

    private Aff<CancelRt, Option<R>> IterateGenePools<R>(
        IGenePoolFactory genePoolFactory,
        Func<IGenePool, Aff<CancelRt, Option<R>>> action)
        where R : class =>
        genePoolFactory.RemotePools.ToSeq().Fold(
            SuccessAff<CancelRt, Option<R>>(None),
            (s, poolName) => from validState in s
                             let genePool = genePoolFactory.CreateNew(poolName)
                             from result in validState.Match(
                                 Some: r => SuccessAff<CancelRt, Option<R>>(r),
                                 None: () => action(genePool))
                             select result);

    public Aff<CancelRt, PrepareGeneResponse> ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from _ in EnsureGene(uniqueGeneId, geneHash, reportProgress)
        let timestamp = DateTimeOffset.UtcNow
        from geneSize in localGenePool.GetCachedGeneSize(uniqueGeneId).ToAff()
        from validGeneSize in geneSize.ToAff(
            Error.New($"The gene {uniqueGeneId} was not properly extracted."))
        select new PrepareGeneResponse
        {
            RequestedGene = uniqueGeneId,
            Inventory = new GeneData()
            {
                Id = uniqueGeneId,
                Hash = geneHash.Value,
                Size = validGeneSize,
            },
            Timestamp = timestamp,
        };

    public Aff<CancelRt, Option<GenePartsInfo>> EnsureGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress) =>
        from _ in SuccessAff(unit)
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from alreadyDownloadedParts in localGenePool.GetDownloadedGeneParts(
            uniqueGeneId,
            geneHash,
            async (processedBytes, totalBytes) =>
            {
                var totalReadMb = Math.Round(processedBytes / 1024d / 1024d, 0);
                var totalMb = Math.Round(totalBytes / 1024d / 1024d, 0);
                var processedPercent = Math.Round(processedBytes / (double)totalBytes, 3);

                var overallPercent = Convert.ToInt32(processedPercent * 75d);

                var progressMessage = $"Verifying downloaded parts of  {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                log.LogTrace("Verifying downloaded parts of {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                    uniqueGeneId, totalReadMb, totalMb, processedPercent);
                await reportProgress(progressMessage, overallPercent);
                return unit;
            })
        select alreadyDownloadedParts;

    // Flow for providing a gene:
    // 1. Check genes.json in local gene pool -> gene is fully merged and available -> done
    // 2. Check local gene pool for already downloaded gene parts and manifest
    //    - produces first part of download progress
    //    - packed
    // 2. Download gene parts

    /*
    private EitherAsync<Error, Unit> EnsureGene(
        string genePoolPath,
        UniqueGeneIdentifier geneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _  in RightAsync<Error, Unit>(unit)
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from geneInfo in localGenePool.RetrieveGene( geneId, geneHash, cancel)
            .MapLeft(error =>
            {
                log.LogDebug(error, "Failed to find gene {GeneId} on local gene pool", geneId);
                return error;
            })
            .BindLeft(_ => ProvideGeneFromRemote(geneId, geneHash, cancel))
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
        from mergedGeneInfo in localGenePool.MergeGeneParts(
                ensuredGeneInfo,
                async (processedBytes, totalBytes) =>
                {
                    var totalReadMb = Math.Round(processedBytes / 1024d / 1024d, 0);
                    var totalMb = Math.Round(totalBytes / 1024d / 1024d, 0);
                    var processedPercent = Math.Round(processedBytes / (double)totalBytes, 3);

                    var overallPercent = Convert.ToInt32(processedPercent * 25d) + 75;

                    var progressMessage = $"Extracting {geneInfo.Id} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                    log.LogTrace("Extracting {Gene} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                        geneInfo.Id, totalReadMb, totalMb, processedPercent);
                    await reportProgress(progressMessage, overallPercent);
                    return unit;
                },
                cancel)
            .MapLeft(e =>
            {
                log.LogInformation(e, "Failed to merge parts of gene {GeneId}", geneId);
                return e;
            })
        select unit;
    */

    /*
    private EitherAsync<Error, GeneInfo> EnsureGeneParts(
        string genePoolPath,
        GeneInfo geneInfo,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _ in RightAsync<Error, Unit>(unit)
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        let totalBytes = geneInfo.Manifest?.Size ?? 0
        // When the gene has already been fetched and extracted, geneInfo.Metadata
        // will just be null. In this case, the following code is mostly skipped
        // as there are no gene parts to ensure.
        from genePartsWithPaths in (geneInfo.Manifest?.Parts).ToSeq()
            .Map(part => from partHash in GenePartHash.NewEither(part).ToAsync()
                         from path in localGenePool.GetGenePartPath(geneInfo.Id, geneInfo.Hash, partHash)
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
    */

    private EitherAsync<Error, GeneInfo> ProvideGeneFromRemote(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancel)
    {
        return ProvideGeneFromRemoteAsync().ToAsync();

        async Task<Either<Error, GeneInfo>> ProvideGeneFromRemoteAsync()
        {

            foreach (var poolName in genepoolFactory.RemotePools)
            {
                var genePool = genepoolFactory.CreateNew(poolName);
                var result = await genePool.RetrieveGene(uniqueGeneId, geneHash, cancel);

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
