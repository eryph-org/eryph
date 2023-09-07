using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Resources;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Genetics
{
    internal class LocalFirstGeneProvider : IGeneProvider
    {
        private readonly IGenePoolFactory _genepoolFactory;
        private readonly ILogger _log;

        public LocalFirstGeneProvider(IGenePoolFactory genepoolFactory, ILogger log)
        {
            _genepoolFactory = genepoolFactory;
            _log = log;
        }

        public EitherAsync<Error, PrepareGeneResponse> ProvideGene(GeneIdentifier geneIdentifier, Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var path = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, "genepool");

            return from genesetInfo in ProvideGeneSet(path, geneIdentifier.GeneSet, reportProgress, Array.Empty<string>(), cancel)
                    .Map(i =>
                    {
                        if (i.Id.Name != geneIdentifier.GeneSet.Name)
                            reportProgress($"Resolved geneset '{geneIdentifier.GeneSet}' as '{i.Id.Name}'");
                        return i;
                    })
                let newGeneIdentifier = geneIdentifier with { GeneSet = genesetInfo.Id }
                from geneHash in GetGeneHash(genesetInfo, newGeneIdentifier)
                from ensuredGene in EnsureGene(genesetInfo, newGeneIdentifier, geneHash, reportProgress, cancel)
                select new PrepareGeneResponse
                {
                    GeneType = geneIdentifier.GeneType,
                    RequestedGene = geneIdentifier.Name,
                    ResolvedGene = ensuredGene.Name
                };

        }

        public EitherAsync<Error, Option<string>> GetGeneSetParent(GeneSetIdentifier genesetIdentifier, Func<string, Task<Unit>> reportProgress, CancellationToken cancellationToken)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();
            var path = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, "genepool");

            return from genesetInfo in ProvideGeneSet(path, genesetIdentifier, reportProgress,Array.Empty<string>(), cancellationToken)
                select string.IsNullOrWhiteSpace(genesetInfo.MetaData.Parent)
                    ? Option<string>.None
                    : Option<string>.Some(genesetInfo.MetaData.Parent);

        }

        private static EitherAsync<Error, string> GetGeneHash(GeneSetInfo genesetInfo, GeneIdentifier geneId)
        {
            string hash = null;
            switch (geneId.GeneType)
            {
                case GeneType.Catlet:
                    hash = genesetInfo.MetaData.CatletGene;
                    break;
                case GeneType.Volume:
                    hash = genesetInfo.MetaData.VolumeGenes?.FirstOrDefault(x => x.Name == geneId.Gene)?.Hash;
                    break;
                case GeneType.Fodder:
                    hash = genesetInfo.MetaData.FodderGenes?.FirstOrDefault(x => x.Name == geneId.Gene)?.Hash;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return string.IsNullOrWhiteSpace(hash)
                ? Error.New($"Could not find {geneId.GeneType.ToString().ToLowerInvariant()} gene {geneId.Gene} in geneset {genesetInfo.Id}")
                : Prelude.RightAsync<Error,string>(hash);

        }


        private EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier geneSetIdentifier,
            Func<string, Task<Unit>> reportProgress, IEnumerable<string> previousRefs, CancellationToken cancel)
        {

            var geneSetRefs = previousRefs as string[] ?? previousRefs.ToArray();
            if (geneSetRefs.Contains(geneSetIdentifier.Name))
            {
                var genesetNames = string.Join('?', geneSetRefs.Append(new[] { geneSetIdentifier.Name })).Replace("?", " => ");
                return Error.New(
                    $"Oops, we have disproved Darwin! A circular reference was found in the following gene sequence: '{genesetNames}'");
 
            }

            var localGenePool = _genepoolFactory.CreateLocal();

            return localGenePool.ProvideGeneSet(path, geneSetIdentifier, cancel) //found locally? (will not resolve 'latest' tag)
                .BindLeft(l =>
                {
                    _log.LogDebug("Failed to find geneset on local gene pool. Local result: {message}", l.Message);

                    return ProvideGeneSetFromRemote(path, geneSetIdentifier, cancel);
                }) // fetch remote image
                .BindLeft(l =>
                {
                    _log.LogDebug("Failed to find geneset on remote gene pools. Remote gene pools result: {message}", l.Message);
                    return localGenePool.ProvideFallbackGeneSet(path, geneSetIdentifier, cancel);
                }) //local fallback (will resolve 'latest' tag)
                .MapLeft(l =>
                {
                    _log.LogInformation("Failed to find geneset on any gene pool. Local fallback result: {message}", l.Message);
                    return Error.New($"Could not find geneset '{geneSetIdentifier.Name}' on any pool.");
                })
                .Bind(genesetInfo => localGenePool.CacheGeneSet(path,genesetInfo, cancel)) // cache anything received in local store
                .Bind(genesetInfo => ResolveGeneSetReferenceAsync(genesetInfo).ToAsync());


            async Task<Either<Error, GeneSetInfo>> ResolveGeneSetReferenceAsync(GeneSetInfo geneSetInfo)
            {
                if (string.IsNullOrWhiteSpace(geneSetInfo.MetaData.Reference))
                    return geneSetInfo;

                //resolve geneset reference
                return await GeneSetIdentifier.Parse(geneSetInfo.MetaData.Reference)
                    .BindAsync(aliasId => ProvideGeneSet(path, aliasId, reportProgress,
                        geneSetRefs.Append(new[] { geneSetIdentifier.Name }), cancel).ToEither());

            }


        }



        private EitherAsync<Error, GeneSetInfo> ProvideGeneSetFromRemote(string path, GeneSetIdentifier genesetId, CancellationToken cancel)
        {
            return ProvideGeneSetFromRemoteAsync().ToAsync();

            async Task<Either<Error, GeneSetInfo>> ProvideGeneSetFromRemoteAsync()
            {
                _log.LogDebug("Trying to find geneset {geneset} on remote pools", genesetId.Name);
                foreach (var sourceName in _genepoolFactory.RemotePools)
                {
                    cancel.ThrowIfCancellationRequested();

                    var genePool = _genepoolFactory.CreateNew(sourceName);
                    var result = await genePool.ProvideGeneSet(path, genesetId, cancel);

                    result.IfLeft(l =>
                    {
                        _log.LogInformation(
                            "Failed to lookup geneset {geneset} on gene pool {genepool}. Message: {message}", genesetId,
                            sourceName, l.Message);
                    });

                    if (result.IsRight)
                    {
                        _log.LogDebug("{geneset} found on gene pool {genepool}", genesetId, sourceName);

                        return result;
                    }
                }

                return Error.New($"could not find geneset {genesetId} on any pool.");
            }
        }

        private EitherAsync<Error, GeneIdentifier> EnsureGene(GeneSetInfo genesetInfo, GeneIdentifier geneId, string geneHash,
            Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            return EnsureGeneAsync().ToAsync();

            async Task<Either<Error, GeneIdentifier>> EnsureGeneAsync()
            {
                var localGenePool = _genepoolFactory.CreateLocal();
                var result = await localGenePool.RetrieveGene(genesetInfo, geneId, geneHash, cancel)
                    .BindLeft(l =>
                        ProvideGeneFromRemote(genesetInfo, geneId, geneHash, cancel))
                    .Bind(geneInfo => localGenePool.CacheGene(geneInfo, genesetInfo, cancel))
                    .Bind(geneInfo => EnsureGeneParts(geneInfo, reportProgress, cancel))
                    .Bind(geneInfo => localGenePool.MergeGenes(geneInfo, genesetInfo, reportProgress, cancel))
                    .ToEither();

                
                result.IfLeft(l =>
                {
                    _log.LogInformation("Failed to retrieve gene {geneId}. Message: {message}", geneId, l.Message);
                });

                return !result.IsRight ? result.Map(_ => geneId) : geneId;
            }
        }

        private EitherAsync<Error, GeneInfo> EnsureGeneParts(GeneInfo geneInfo, Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            return EnsureGenePartsAsync().ToAsync();

            async Task<Either<Error, GeneInfo>> EnsureGenePartsAsync()
            {

                var localGenePool = _genepoolFactory.CreateLocal();
                var parts = (geneInfo.MetaData?.Parts ?? Array.Empty<string>()).ToList();
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

                foreach (var poolName in _genepoolFactory.RemotePools)
                {
                    var genePool = _genepoolFactory.CreateNew(poolName);
                    var result = await genePool.RetrieveGene(genesetInfo, geneIdentifier, geneHash, cancel);

                    result.IfLeft(l =>
                    {
                        _log.LogInformation("Failed to retrieve gene {gene} on pool {pool}. Message: {message}",
                            geneIdentifier, poolName, l.Message);
                    });

                    if (result.IsRight)
                        return result;

                }

                return Error.New($"Could not find gene {geneIdentifier} on any remote pool.");
            }


        }


        private EitherAsync<Error, long> ProvideGenePartFromRemote(
            GeneInfo geneInfo, string genePart, long availableSize, long totalSize, Func<string, Task<Unit>> reportProgress,
            Stopwatch stopwatch, CancellationToken cancel)
        {
            return ProvideGenePartFromRemoteAsync().ToAsync();

            async Task<Either<Error, long>> ProvideGenePartFromRemoteAsync()
            {
                foreach (var poolName in _genepoolFactory.RemotePools)
                {
                    var imageSource = _genepoolFactory.CreateNew(poolName);
                    var result = await imageSource.RetrieveGenePart(geneInfo, genePart, availableSize, totalSize,
                        reportProgress, stopwatch, cancel);

                    result.IfLeft(l =>
                    {
                        _log.LogInformation(
                            "Failed to retrieve gene part {genePart} of gene {gene} on source {source}. Message: {message}",
                            genePart, geneInfo, poolName, l.Message);
                    });

                    if (result.IsRight)
                        return result;

                }

                return Error.New($"Could not find gene part {genePart} of gene {geneInfo}  on any remote source.");
            }
        }
    }
}
