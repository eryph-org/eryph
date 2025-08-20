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
using Eryph.GenePool.Model;
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
    public Aff<CancelRt, GenesetTagManifestData> GetGeneSetManifest(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from cachedManifest in localGenePool.GetCachedGeneSet(geneSetId)
        from pulledGeneSet in cachedManifest
            // gene set references should always be checked online
            // TODO add special error handling which ignores all error when we force online lookup
            .Filter(cm => string.IsNullOrWhiteSpace(cm.Reference))
            .Match(
                Some: g => SuccessAff<CancelRt, Option<GenesetTagManifestData>>(Some(g)),
                None: () => PullAndCacheGeneSet(geneSetId))
        from result in (pulledGeneSet | cachedManifest).ToAff(
            Error.New($"The gene set {geneSetId} is not available in local or remote gene pools."))
        select result;

    private Aff<CancelRt, Option<GenesetTagManifestData>> PullAndCacheGeneSet(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from pulledGeneSet in IterateGenePools(
                genepoolFactory,
                pool => pool.GetGeneSet(geneSetId))
            .Catch(
                e => !IsUnexpectedHttpClientError(e),
                e =>
                {
                    log.LogInformation(e, "Failed to lookup gene set {GeneSetId} on all gene pools.", geneSetId);
                    return SuccessAff<CancelRt, Option<GeneSetInfo>>(None);
                })
        from _ in pulledGeneSet
            .Map(localGenePool.CacheGeneSet)
            .Sequence()
        select pulledGeneSet.Map(gi => gi.Manifest);

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
            .Map(localGenePool.CacheGeneContent)
            .Sequence()
        select content;

    public Aff<CancelRt, PrepareGeneResponse> ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress) =>
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from _ in EnsureGene(uniqueGeneId, geneHash, reportProgress)
        let timestamp = DateTimeOffset.UtcNow
        from geneSize in localGenePool.GetCachedGeneSize2(uniqueGeneId)
        from validGeneSize in geneSize.ToAff(
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

    public Aff<CancelRt, Unit> EnsureGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<string, int, Task<Unit>> reportProgress) =>
        from _1 in SuccessAff(unit)
        from genePoolPath in genePoolPathProvider.GetGenePoolPath().ToAff()
        let localGenePool = genepoolFactory.CreateLocal(genePoolPath)
        from localGeneParts in localGenePool.GetDownloadedGeneParts(
            uniqueGeneId,
            geneHash,
            async (processedBytes, totalBytes) =>
            {
                var totalReadMb = Math.Round(processedBytes / 1024d / 1024d, 0);
                var totalMb = Math.Round(totalBytes / 1024d / 1024d, 0);
                var processedPercent = Math.Round(processedBytes / (double)totalBytes, 3);

                var overallPercent = Convert.ToInt32(processedPercent * 50d);

                var progressMessage = $"Verifying downloaded parts of {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                log.LogTrace("Verifying downloaded parts of {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                    uniqueGeneId, totalReadMb, totalMb, processedPercent);
                await reportProgress(progressMessage, overallPercent);
                return unit;
            })
        from hasMergedGene in localGenePool.HasGene(uniqueGeneId, geneHash)
        let isDownloadComplete = !localGeneParts.IsEmpty && localGeneParts.Values.ToSeq().All(s => s.IsSome)
        let existingGeneParts = localGeneParts.ToSeq()
            .Map(kvp => kvp.Value.Map(s => (kvp.Key, s)))
            .Somes()
        from _2 in hasMergedGene || isDownloadComplete
            ? SuccessAff<CancelRt, Unit>(unit)
            : use(
                SuccessAff<CancelRt, GenePartsState>(new GenePartsState()),
                partsState =>
                    from _ in localGeneParts.ToSeq()
                        .Map(kvp => kvp.Value.Map(s => (Part: kvp.Key, Size: s)))
                        .Somes()
                        .Map(pi => partsState.AddPart(pi.Part, pi.Size))
                        .SequenceSerial()
                    from result in retry(
                        Schedule.NoDelayOnFirst & Schedule.spaced(TimeSpan.FromSeconds(2)) & Schedule.recurs(5),
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

                                    var overallPercent = Convert.ToInt32(processedPercent * 50d);

                                    var progressMessage = $"Downloading parts of {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                                    log.LogTrace("Downloading parts of {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                                        uniqueGeneId, totalReadMb, totalMb, processedPercent);
                                    await reportProgress(progressMessage, overallPercent);
                                    return unit;
                                })))
                    from _2 in result.ToAff(Error.New($"The gene {uniqueGeneId} ({geneHash}) is not available on any remote gene pool."))
                    select unit)
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
                    
                    var overallPercent = Convert.ToInt32(processedPercent * 50d + 50d);
                    
                    var progressMessage = $"Extracting {uniqueGeneId} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {processedPercent:P1} completed";
                    log.LogTrace("Extracting {GeneId} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                        uniqueGeneId, totalReadMb, totalMb, processedPercent);
                    await reportProgress(progressMessage, overallPercent);
                    return unit;
                })
        select unit;

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
