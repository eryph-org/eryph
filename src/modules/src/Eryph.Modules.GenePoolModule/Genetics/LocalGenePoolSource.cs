using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.GenePool.Compression;
using Eryph.GenePool.Model;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Medallion.Threading;
using Medallion.Threading.FileSystem;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenePool.Genetics;

/// <summary>
/// Handles the storage of genes in the local file system.
/// </summary>
/// <remarks>
/// <para>
/// This class must be scoped or transient. It caches the gene pool path
/// provided by <see cref="IGenePoolPathProvider"/> for performance
/// reasons but that path can change at runtime.
/// </para>
/// </remarks>
[UsedImplicitly]
internal class LocalGenePoolSource(
    IFileSystemService fileSystem,
    IGenePoolPathProvider genePoolPathProvider,
    ILogger log)
    : ILocalGenePool
{
    private readonly Aff<String> _genePoolPathMemo = genePoolPathProvider.GetGenePoolPath().Memo();

    private const string GenesFileName = "genes.json";

    private const int BufferSize = 1 * 1024 * 1024;

    public Aff<string> GetTempGenePath(UniqueGeneIdentifier uniqueGeneId, GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetTempGenePath(genePoolPath, uniqueGeneId, geneHash)
        select genePath;

    public Aff<CancelRt, Option<GenesetTagManifestData>> GetCachedGeneSet(
    GeneSetIdentifier geneSetId) =>
    from genePoolPath in _genePoolPathMemo
    let manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId)
    from manifest in use(AcquireGeneSetLock(geneSetId), _ =>
        Aff<CancelRt, Option<GenesetTagManifestData>>(async rt =>
        {
            if (!fileSystem.FileExists(manifestPath))
                return None;

            await using var manifestStream = fileSystem.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(
                manifestStream,
                GeneModelDefaults.SerializerOptions,
                cancellationToken: rt.CancellationToken);

            if (manifest is null)
                throw Error.New($"Could not deserialize the manifest of the gene set {geneSetId}.");

            return Some(manifest);
        }))
    select manifest;

    public Aff<CancelRt, Option<string>> GetCachedGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from content in use(AcquireGeneLock(uniqueGeneId), _ =>
            Aff<CancelRt, Option<string>>(async rt =>
            {
                if (!fileSystem.FileExists(genePath))
                    return Option<string>.None;
                var content = await fileSystem.ReadAllTextAsync(genePath, rt.CancellationToken);

                return Some(content);
            }))
        from mergedGenes in GetMergedGenes(uniqueGeneId.Id.GeneSet)
        select content.Filter(_ => mergedGenes.Contains(geneHash));

    public Aff<CancelRt, HashMap<GenePartHash, Option<long>>> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<long, long, Task> reportProgress) =>
        from geneParts in use(AcquireGeneLock(uniqueGeneId), _ =>
            from manifest in ReadTempGeneManifest(uniqueGeneId, geneHash)
            from geneParts in manifest
                .Map(m => GetDownloadedGeneParts(uniqueGeneId, geneHash, m, reportProgress))
                .Sequence()
            select geneParts.IfNone(Empty))
        select geneParts;

    private Aff<CancelRt, HashMap<GenePartHash, Option<long>>> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GeneManifestData manifest,
        Func<long, long, Task> reportProgress) =>
        from geneParts in GeneManifestUtils.GetParts(manifest).ToAff()
        from totalBytes in Optional(manifest.Size).ToAff(
            Error.New($"The gene manifest of {uniqueGeneId} ({geneHash.Hash}) does not contain a size."))
        from result in geneParts.Fold(
            SuccessAff<CancelRt, HashMap<GenePartHash, Option<long>>>(Empty),
            (state, part) => from partInfos in state
                from partInfo in GetDownloadedGenePart(uniqueGeneId, geneHash, part)
                let result = partInfos.Add(part, partInfo)
                let availableBytes = result.Values.Somes().Sum()
                from _ in Aff<CancelRt, Unit>(async _ =>
                {
                    await reportProgress(availableBytes, totalBytes);
                    return unit;
                })
                select result)
        select result;

    private Aff<CancelRt, Option<long>> GetDownloadedGenePart(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartHash genePartHash) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetTempGenePath(genePoolPath, uniqueGeneId, geneHash)
        let genePartPath = GenePoolPaths.GetTempGenePartPath(genePath, genePartHash)
        from size in Aff<CancelRt, Option<long>>(async rt =>
        {
            if (!fileSystem.FileExists(genePartPath))
                return None;

            using var hashAlgorithm = genePartHash.CreateAlgorithm();
            await using (var dataStream = fileSystem.OpenRead(genePartPath))
            {
                // Use a buffered stream as HashAlgorithm.ComputeHashAsync()
                // uses an extremely small buffer (4096 bytes) which slows
                // down the hashing operation significantly.
                await using var bufferedStream = new BufferedStream(dataStream, BufferSize);
                await hashAlgorithm.ComputeHashAsync(bufferedStream, rt.CancellationToken);
            }
            
            var actualGenePartHash = hashAlgorithm.ToGenePartHash();

            if (actualGenePartHash.Hash == genePartHash.Hash)
                return Some(fileSystem.GetFileSize(genePartPath));

            fileSystem.FileDelete(genePartPath);
            return None;
        })
        select size;

    public Aff<CancelRt, Unit> MergeGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<long, long, Task> reportProgress) =>
        from genePoolPath in _genePoolPathMemo
        from _ in use(AcquireGeneLock(uniqueGeneId), _ =>
            // We are going to re(write) the gene on the disk. Remove it from the merged genes
            // until merge has been completed successfully.
            from _1 in RemoveMergedGene(uniqueGeneId, geneHash)
            from optionalManifest in ReadTempGeneManifest(uniqueGeneId, geneHash)
            from manifest in optionalManifest.ToAff(
                Error.New($"The gene manifest of {uniqueGeneId} ({geneHash.Hash}) is missing."))
            from geneParts in GeneManifestUtils.GetParts(manifest).ToAff()
            from originalSize in Optional(manifest.OriginalSize).ToAff(
                Error.New($"The gene manifest of {uniqueGeneId} ({geneHash.Hash}) does not contain the original size."))
            from format in Optional(manifest.Format).ToAff(
                Error.New($"The gene manifest of {uniqueGeneId} ({geneHash.Hash}) does not contain the format."))
            let geneTempPath = GenePoolPaths.GetTempGenePath(genePoolPath, uniqueGeneId, geneHash)
            let tempPartPaths = geneParts.Map(p => GenePoolPaths.GetTempGenePartPath(geneTempPath, p))
            let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
            from _2 in Aff<CancelRt, Unit>(async rt =>
            {
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(genePath));
                var streams = new List<Stream>();
                try
                {
                    foreach (var partPath in tempPartPaths)
                    {
                        streams.Add(fileSystem.OpenRead(partPath));
                    }

                    await using var multiStream = new MultiStream(streams);
                    await using var decompressionStream = CompressionStreamFactory.CreateDecompressionStream(
                        multiStream, format);

                    await using var fileStream = fileSystem.OpenWrite(genePath);
                    await using var progressStream = new ProgressStream(
                        fileStream,
                        TimeSpan.FromSeconds(10),
                        async (writtenBytes, _) => await reportProgress(writtenBytes, originalSize));

                    await decompressionStream.CopyToAsync(progressStream, BufferSize, rt.CancellationToken);
                    return unit;
                }
                finally
                {
                    foreach (var stream in streams)
                    {
                        await stream.DisposeAsync();
                    }
                }
            })
            from _3 in AddMergedGene(uniqueGeneId, geneHash)
            from _4 in Eff(() =>
            {
                fileSystem.DeleteDirectory(geneTempPath);
                return unit;
            })
            select unit)
        select unit;

    public Aff<CancelRt, Unit> CacheGeneSet(GeneSetInfo geneSetInfo) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetInfo.Id)
        let manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetInfo.Id)
        from genes in GeneSetTagManifestUtils.GetGenes(geneSetInfo.Manifest)
            .ToAff(e => Error.New($"The manifest of the gene set {geneSetInfo.Id} contains invalid genes.", e))
        from _1 in use(AcquireGeneSetLock(geneSetInfo.Id), _ =>
            Aff<CancelRt, Unit>(async rt =>
            {
                fileSystem.EnsureDirectoryExists(geneSetPath);
                await using var manifestStream = fileSystem.OpenWrite(manifestPath);
                await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.Manifest,
                    GeneModelDefaults.SerializerOptions, rt.CancellationToken);
                return unit;
            }))
        // The remote gene pool includes small genes (catlets, fodder) directly in the
        // gene set tag response. This provides a prefetch mechanism to reduce the
        // number of requests to the remote gene pool.
        let contentMap = geneSetInfo.GeneDownloadInfo.ToSeq()
            .Map(dr => from hash in Gene.NewOption(dr.Gene)
                from content in Optional(dr.Content).Filter(notEmpty)
                select (hash, content))
            .Somes()
            .ToHashMap()
        from _ in genes.ToSeq()
            .Filter(kvp => kvp.Key.GeneType is GeneType.Catlet or GeneType.Fodder)
            .Map(kvp => CacheGene(kvp.Key, kvp.Value, contentMap))
            .SequenceSerial()
        select unit;

    private Aff<CancelRt, Unit> CacheGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        HashMap<Gene, string> contentMap) =>
        from _ in contentMap.Find(geneHash.ToGene())
            .Map(content => CacheGene(uniqueGeneId, geneHash, content))
            .Sequence()
        select unit;

    private Aff<CancelRt, Unit> CacheGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        string content) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        // This logic is only triggered when the gene was included when a downloaded
        // gene set tag manifest. The manifest is cached and must be downloaded before
        // any genes are downloaded. Hence, the gene can only exist if the user modified
        // the local gene pool.
        from _ in use(AcquireGeneLock(uniqueGeneId), _ =>
            from _1 in RemoveMergedGene(uniqueGeneId, geneHash)
            from _2 in Aff<CancelRt, Unit>(async rt =>
            {
                if (fileSystem.FileExists(genePath))
                    fileSystem.DeleteFile(genePath);

                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(genePath));
                await fileSystem.WriteAllTextAsync(genePath, content, rt.CancellationToken);
                return unit;
            })
            from _3 in AddMergedGene(uniqueGeneId, geneHash)
            select unit)
        select unit;

    public Aff<CancelRt, string> CacheGeneContent(
        GeneContentInfo geneContentInfo) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneContentInfo.UniqueId)
        from content in use(AcquireGeneLock(geneContentInfo.UniqueId), _ =>
            // We are going to re(write) the gene on the disk. Remove it from the merged genes
            // until the merge has been completed successfully.
            from _1 in RemoveMergedGene(geneContentInfo.UniqueId, geneContentInfo.Hash)
            from content in Aff<CancelRt, string>(async rt =>
            {
                if (fileSystem.FileExists(genePath))
                    fileSystem.DeleteFile(genePath);

                var geneFolder = Path.GetDirectoryName(genePath);
                fileSystem.EnsureDirectoryExists(geneFolder);
                await using var sourceStream = new MemoryStream(geneContentInfo.Content);
                await using var decompressionStream = CompressionStreamFactory.CreateDecompressionStream(
                    sourceStream, geneContentInfo.Format);
                await using var targetStream = new MemoryStream();
                using var reader = new StreamReader(decompressionStream, new UTF8Encoding(false));
                var content = await reader.ReadToEndAsync(rt.CancellationToken);
                await fileSystem.WriteAllTextAsync(genePath, content, rt.CancellationToken);
                return content;
            })
            from _2 in AddMergedGene(geneContentInfo.UniqueId, geneContentInfo.Hash)
            select content)
        select content;

    public Aff<CancelRt, Option<long>> GetGeneSize(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from geneExists in Eff(() => fileSystem.FileExists(genePath))
        from fileSize in geneExists
            ? Eff(() => fileSystem.GetFileSize(genePath)).Map(Optional)
            : SuccessEff<Option<long>>(None)
        from mergedGenes in GetMergedGenes(uniqueGeneId.Id.GeneSet)
        let isMerged = mergedGenes.Contains(geneHash)
        select fileSize.Filter(_ => isMerged);

    public Aff<CancelRt, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in SuccessAff(unit)
        from manifest in GetCachedGeneSet(uniqueGeneId.Id.GeneSet)
        from genes in manifest
            .Map(GeneSetTagManifestUtils.GetGenes)
            .Sequence()
            .ToAff()
        let geneHash = genes.Bind(g => g.Find(uniqueGeneId))
        from _2 in geneHash
            .Map(h => RemoveCachedGene(uniqueGeneId, h))
            .SequenceSerial()
        select unit;

    private Aff<CancelRt, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        from _ in use(AcquireGeneLock(uniqueGeneId), _ =>
            from _1 in RemoveMergedGene(uniqueGeneId, geneHash)
            let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
            from _3 in Eff(() =>
            {
                if (!fileSystem.FileExists(genePath)) 
                    return unit;
             
                fileSystem.DeleteFile(genePath);
                return unit;
            })
            select unit)
        select unit;

    private Aff<CancelRt, Option<GeneManifestData>> ReadTempGeneManifest(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let genePath = GenePoolPaths.GetTempGenePath(genePoolPath, uniqueGeneId, geneHash)
        let manifestPath = GenePoolPaths.GetTempGeneManifestPath(genePath)
        from manifest in Aff<CancelRt, Option<GeneManifestData>>(async rt =>
        {
            if (!fileSystem.FileExists(manifestPath))
                return None;

            var json = await fileSystem.ReadAllTextAsync(manifestPath, rt.CancellationToken);
            return Some(JsonSerializer.Deserialize<GeneManifestData>(json, GeneModelDefaults.SerializerOptions));
        })
        let actualGeneHash = manifest.Map(GeneManifestUtils.ComputeHash)
        from _2 in guard(
            actualGeneHash.IsNone || actualGeneHash == geneHash,
            Error.New(
                $"The manifest of the gene {uniqueGeneId} ({geneHash.Hash}) in the local gene pool is corrupted."))
        select manifest;

    private Aff<CancelRt, Seq<GeneHash>> GetMergedGenes(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId)
        from hashes in use(AcquireGeneSetLock(geneSetId), _ =>
            Aff(async () =>
            {
                var genesInfo = await ReadGenesInfo(geneSetPath);
                return genesInfo.MergedGenes.ToSeq();
            }))
        from result in hashes
            .Map(GeneHash.NewValidation)
            .Sequence()
            .ToAff(errors => Error.New($"The merged genes of the gene set '{geneSetId}' are invalid.", Error.Many(errors)))
        select result;

    private Aff<CancelRt, Unit> AddMergedGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from _ in use(AcquireGeneSetLock(uniqueGeneId.Id.GeneSet), _ =>
            Aff(async () =>
            {
                var genesInfo = await ReadGenesInfo(geneSetPath);
                genesInfo.MergedGenes = toSet(genesInfo.MergedGenes).Add(geneHash.Value).ToList();
                await WriteGenesInfo(geneSetPath, genesInfo);
                return unit;
            }))
        select unit;

    private Aff<CancelRt, Unit> RemoveMergedGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from _ in use(AcquireGeneSetLock(uniqueGeneId.Id.GeneSet), _ =>
            Aff(async () =>
            {
                var genesInfo = await ReadGenesInfo(geneSetPath);
                genesInfo.MergedGenes = toSet(genesInfo.MergedGenes).Remove(geneHash.Value).ToList();
                await WriteGenesInfo(geneSetPath, genesInfo);
                return unit;
            }))
        select unit;

    private async Task<GenesInfo> ReadGenesInfo(string geneSetPath)
    {
        if (!fileSystem.FileExists(Path.Combine(geneSetPath, GenesFileName)))
            return new GenesInfo();

        var json = await fileSystem.ReadAllTextAsync(Path.Combine(geneSetPath, GenesFileName), CancellationToken.None);
        return JsonSerializer.Deserialize<GenesInfo>(json);
    }

    private async Task WriteGenesInfo(string geneSetPath, GenesInfo genesInfo)
    {
        var json = JsonSerializer.Serialize(genesInfo);
        await fileSystem.WriteAllTextAsync(Path.Combine(geneSetPath, GenesFileName), json, CancellationToken.None);
    }

    private class GenesInfo
    {
        public IReadOnlyList<string> MergedGenes { get; set; } = [];
    }

    private Aff<CancelRt, IDistributedSynchronizationHandle> AcquireGeneSetLock(
        GeneSetIdentifier geneSetId) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetLockPath = GenePoolPaths.GetGeneSetLockPath(genePoolPath, geneSetId)
        from @lock in Aff<CancelRt, IDistributedSynchronizationHandle>(
            async rt => await new FileDistributedLock(new FileInfo(geneSetLockPath))
                .AcquireAsync(cancellationToken: rt.CancellationToken))
        select @lock;

    private Aff<CancelRt, IDistributedSynchronizationHandle> AcquireGeneLock(
        UniqueGeneIdentifier uniqueGeneId) =>
        from genePoolPath in _genePoolPathMemo
        let geneSetLockPath = GenePoolPaths.GetGeneLockPath(genePoolPath, uniqueGeneId)
        from @lock in Aff<CancelRt, IDistributedSynchronizationHandle>(
            async rt => await new FileDistributedLock(new FileInfo(geneSetLockPath))
                .AcquireAsync(cancellationToken: rt.CancellationToken))
        select @lock;
}
