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
using LanguageExt.UnsafeValueAccess;
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
        Func<IGenePool, Aff<CancelRt, Option<R>>> action) =>
        genePoolFactory.RemotePools.ToSeq().Fold(
            SuccessAff<CancelRt, Option<R>>(None),
            (state, poolName) => state.BiBind(
                Succ: o => o.Match(
                    // We have a valid result, nothing more to do
                    Some: r => SuccessAff<CancelRt, Option<R>>(r),
                    // No result, try next pool
                    None: () => action(genePoolFactory.CreateNew(poolName))),
                Fail: e => IsUnexpectedHttpClientError(e)
                    // If the error is an HTTP client, we immediately fail. This way,
                    // authentication errors are always propagated to the caller.
                    ? FailAff<CancelRt, Option<R>>(e)
                    : from o in action(genePoolFactory.CreateNew(poolName))
                      // When an error occurred, we still try the next pool. When the next pool
                      // does not contain the gene, we return the last error. This way, one error
                      // is propagated to the caller.
                      from r in o.ToAff(e)
                      select Some(r)));

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

    public Aff<CancelRt, Unit> EnsureGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress) =>
        from _1 in SuccessAff(unit)
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

                var progressMessage = $"Verifying downloaded parts of {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                log.LogTrace("Verifying downloaded parts of {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                    uniqueGeneId, totalReadMb, totalMb, processedPercent);
                await reportProgress(progressMessage, overallPercent);
                return unit;
            })
        from hasMergedGene in localGenePool.HasGene(uniqueGeneId, geneHash)
        let isDownloadComplete = alreadyDownloadedParts.IsComplete
        from _2 in hasMergedGene || isDownloadComplete
            ? SuccessAff<CancelRt, Unit>(unit)
            : use(
                SuccessAff<CancelRt, GenePartsState>(new GenePartsState()),
                partsState =>
                    from _ in alreadyDownloadedParts.Parts.ToSeq()
                        .Map(kvp => partsState.AddPart(kvp.Key, kvp.Value))
                        .SequenceSerial()
                    from result in retry(
                        // TODO fix schedule
                        Schedule.repeat(5),
                        IterateGenePools(
                        genepoolFactory,
                        genePool => genePool.DownloadGene2(
                            uniqueGeneId,
                            geneHash,
                            partsState,
                            GenePoolPaths.GetTempGenePath(genePoolPath, uniqueGeneId, geneHash),
                            async (long processedBytes, long totalBytes) =>
                            {
                                var totalReadMb = Math.Round(processedBytes / 1024d / 1024d, 0);
                                var totalMb = Math.Round(totalBytes / 1024d / 1024d, 0);
                                var processedPercent = Math.Round(processedBytes / (double)totalBytes, 3);

                                var overallPercent = Convert.ToInt32(processedPercent * 75d);

                                var progressMessage = $"Downloading parts of {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                                log.LogTrace("Downloading parts of {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                                    uniqueGeneId, totalReadMb, totalMb, processedPercent);
                                await reportProgress(progressMessage, overallPercent);
                                return unit;
                            })))
                    from _2 in result.ToAff(Error.New($"The gene {uniqueGeneId} ({geneHash}) is not available on any remote gene pool."))
                    select unit)

            // TODO skip download if local parts are complete
            // TODO check if gene is already merged
        from _4 in hasMergedGene && !isDownloadComplete
            ? SuccessAff<CancelRt, Unit>(unit)
            : localGenePool.MergeGene2(
                uniqueGeneId,
                geneHash,
                async (processedBytes, totalBytes) =>
                {
                    var totalReadMb = Math.Round(processedBytes / 1024d / 1024d, 0);
                    var totalMb = Math.Round(totalBytes / 1024d / 1024d, 0);
                    var processedPercent = Math.Round(processedBytes / (double)totalBytes, 3);
                    
                    var overallPercent = Convert.ToInt32(processedPercent * 25d) + 75;
                    
                    var progressMessage = $"Extracting {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                    log.LogTrace("Extracting {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                        uniqueGeneId, totalReadMb, totalMb, processedPercent);
                    await reportProgress(progressMessage, overallPercent);
                    return unit;
                })
        select unit;



    // Flow for providing a gene:
    // 1. Check genes.json in local gene pool -> gene is fully merged and available -> done
    // 2. Check local gene pool for already downloaded gene parts and manifest
    //    - produces first part of download progress
    //    - packed
    // 2. Download gene parts

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
