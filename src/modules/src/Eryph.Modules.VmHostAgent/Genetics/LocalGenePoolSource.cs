using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.VmHostAgent.Genetics;

[UsedImplicitly]
internal class LocalGenePoolSource(
    IFileSystemService fileSystem,
    ILogger log,
    string poolName,
    string genePoolPath)
    : GenePoolBase, ILocalGenePool
{
    private const string GenesFileName = "genes.json";

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        GeneSetInfo geneSetInfo,
        UniqueGeneIdentifier uniqueGeneId,
        string geneHash,
        CancellationToken cancel) =>
        from parsedGeneHash in ParseGeneHash(geneHash).ToAsync()
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from genesInfo in TryAsync(() => ReadGenesInfo(geneSetPath)).ToEither()
        from geneInfo in genesInfo.MergedGenes.ToSeq().Find(h => h == geneHash).Match(
            Some: h => new GeneInfo(uniqueGeneId, geneHash, null,
                [], DateTimeOffset.MinValue, null, true),
            None: () =>
                from _ in RightAsync<Error, Unit>(unit)
                let genePath = GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, parsedGeneHash.Hash)
                let cachedGeneManifestPath = Path.Combine(genePath, "gene.json")
                from manifestExists in Try(() => fileSystem.FileExists(cachedGeneManifestPath))
                    .ToEitherAsync()
                from _2 in guard(manifestExists,
                    Error.New($"The gene '{uniqueGeneId}' is not available on the local gene pool."))
                from manifestJson in Try(() =>
                {
                    using var hashAlg = CreateHashAlgorithm(parsedGeneHash.HashAlg);

                    var manifestJsonData = fileSystem.ReadText(cachedGeneManifestPath);
                    var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));
                    if (hashString == parsedGeneHash.Hash)
                        return manifestJsonData;
                    
                    fileSystem.FileDelete(cachedGeneManifestPath);
                    throw new VerificationException($"Failed to verify the hash of the manifest of '{uniqueGeneId}'");
                }).ToEitherAsync()
                from manifestData in Try(() => JsonSerializer.Deserialize<GeneManifestData>(manifestJson))
                    .ToEitherAsync()
                select new GeneInfo(uniqueGeneId, parsedGeneHash.Hash,
                    manifestData, [], DateTimeOffset.MinValue, genePath, false))
        select geneInfo;

    public EitherAsync<Error, long> RetrieveGenePart(
        GeneInfo geneInfo,
        string genePartHash,
        string genePartPath,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel) =>
        from parsedHash in ParseGenePartHash(genePartHash).ToAsync()
        from partExists in Try(() => fileSystem.FileExists(genePartPath))
            .ToEitherAsync()
        from _2 in guard(partExists,
            Error.New($"The gene part '{geneInfo}/{parsedHash.Hash[..12]}' is not available on the local gene pool."))
        from isHashValid in TryAsync(async () =>
        {
            using var hashAlg = CreateHashAlgorithm(parsedHash.HashAlg);
            await using var dataStream = File.OpenRead(genePartPath);

            await hashAlg.ComputeHashAsync(dataStream, cancel);
            var hashString = GetHashString(hashAlg.Hash);

            return hashString == parsedHash.Hash;
        }).ToEither()
        from _3 in guard(isHashValid,
            Error.New($"Failed to verify the hash of the gene part '{geneInfo}/{parsedHash.Hash[..12]}'."))
        from partSize in Try(() => fileSystem.GetFileSize(genePartPath))
            .ToEitherAsync()
        select partSize;

    public string PoolName => poolName;

    public EitherAsync<Error, Unit> MergeGeneParts(
        GeneInfo geneInfo,
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneInfo.Id.Id.GeneSet);
            var mergedGenesInfo = await ReadGenesInfo(geneSetPath);

            var mergedGenes = mergedGenesInfo.MergedGenes ?? [];

            if (mergedGenes.Contains(geneInfo.Hash) || geneInfo.LocalPath == null)
                return Unit.Default;

            var parts = geneInfo.MetaData?.Parts ?? [];

            var streams = parts.Map(part =>
            {
                var partHash = part.Split(':').Last();
                var path = Path.Combine(geneInfo.LocalPath, $"{partHash}.part");
                return fileSystem.OpenRead(path);
            }).ToArray();
            try
            {
                await using var multiStream = new MultiStream(streams);
                var decompression = new GeneDecompression(geneInfo, fileSystem, log, reportProgress);
                await decompression.Decompress(multiStream, genePoolPath, cancel);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }

            mergedGenesInfo = await ReadGenesInfo(geneSetPath);
            mergedGenesInfo.MergedGenes = mergedGenesInfo.MergedGenes
                .Append([geneInfo.Hash])
                .ToArray();
            await WriteGenesInfo(geneSetPath, mergedGenesInfo);

            fileSystem.DeleteDirectory(geneInfo.LocalPath);

            return unit;
        }).ToEither();
    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        GeneSetIdentifier geneSetIdentifier,
        CancellationToken cancel) =>
        TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetIdentifier);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetIdentifier);
            if (!fileSystem.FileExists(geneSetManifestPath))
                return await LeftAsync<Error, GeneSetInfo>(Error.New(
                    $"Geneset '{geneSetIdentifier.Value}' not found in local gene pool.")).ToEither();

            await using var manifestStream = File.OpenRead(geneSetManifestPath);
            var manifest =
                await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancel);

            return Right<Error, GeneSetInfo>(new GeneSetInfo(geneSetIdentifier, geneSetPath, manifest, []));

        })
        .ToEither()
        .Bind(e => e.ToAsync());

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(
        GeneSetInfo geneSetInfo,
        CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetInfo.Id);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetInfo.Id);
            fileSystem.EnsureDirectoryExists(geneSetPath);
            
            await using var manifestStream = fileSystem.OpenWrite(geneSetManifestPath);
            await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.MetaData, cancellationToken: cancel);
            return new GeneSetInfo(geneSetInfo.Id, geneSetPath, geneSetInfo.MetaData,
                geneSetInfo.GeneDownloadInfo);

        }).ToEither();
    }

    public EitherAsync<Error, GeneInfo> CacheGene(
        GeneInfo geneInfo,
        GeneSetInfo geneSetInfo,
        CancellationToken cancel)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return from parsedGeneHash in ParseGeneHash(geneInfo.Hash).ToAsync()
               from result in TryAsync(async () =>
               {
                   var genePath = GetGeneTempPath(genePoolPath, geneInfo.Id.Id.GeneSet, parsedGeneHash.Hash);
                   fileSystem.EnsureDirectoryExists(genePath);
               
                   await using var manifestStream = fileSystem.OpenWrite(Path.Combine(genePath, "gene.json"));
                   await JsonSerializer.SerializeAsync(manifestStream, geneInfo.MetaData, cancellationToken: cancel);
                   return new GeneInfo(geneInfo.Id, geneInfo.Hash, geneInfo.MetaData,
                       geneInfo.DownloadUris, geneInfo.DownloadExpires,
                       genePath,
                       false);
               
               }).ToEither()
               select result;
    }

    public EitherAsync<Error, GeneSetInfo> GetCachedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId)
        let manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId)
        from manifestExists in Try(() => fileSystem.FileExists(manifestPath))
            .ToEitherAsync()
        from __ in guard(manifestExists, Error.New("The gene set does not exist"))
        from manifest in TryAsync(async () =>
        {
            await using var manifestStream = fileSystem.OpenRead(manifestPath);
            return await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancellationToken);
        }).ToEither()
        select new GeneSetInfo(geneSetId, geneSetPath, manifest, []);

    public EitherAsync<Error, Option<long>> GetCachedGeneSize(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in RightAsync<Error, Unit>(unit)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from geneExists in Try(() => fileSystem.FileExists(genePath))
            .ToEitherAsync()
        from fileSize in geneExists
            ? Try(() => fileSystem.GetFileSize(genePath)).ToEitherAsync().Map(Optional)
            : RightAsync<Error, Option<long>>(None)
        select fileSize;

    public EitherAsync<Error, string> GetGenePartPath(
        UniqueGeneIdentifier uniqueGeneId,
        string geneHash,
        string genePartHash) =>
        from parsedGeneHash in ParseGeneHash(geneHash).ToAsync()
        from parsedPartHash in ParseGenePartHash(genePartHash).ToAsync()
        // TODO ensure that the path exists?
        let geneTempPath = GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, parsedGeneHash.Hash)
        let genePartPath = Path.Combine(geneTempPath, $"{parsedPartHash.Hash}.part")
        select genePartPath;

    public EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet);
            var manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, uniqueGeneId.Id.GeneSet);
            if (!fileSystem.FileExists(manifestPath))
                return unit;

            var manifestJson = await fileSystem.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<GenesetTagManifestData>(manifestJson);

            var geneHash = GeneSetManifestUtils.FindGeneHash(
                manifest, uniqueGeneId.GeneType, uniqueGeneId.Architecture, uniqueGeneId.Id.GeneName);
            if (geneHash.IsNone)
                return unit;

            var genes = await RemoveMergedGene(geneSetPath, geneHash.ValueUnsafe());
            
            var genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId);
            if (fileSystem.FileExists(genePath))
            {
                fileSystem.DeleteFile(genePath);
            }

            if (genes.MergedGenes is null || genes.MergedGenes.Length == 0)
            {
                fileSystem.DeleteDirectory(geneSetPath);
            }

            return unit;
        }).ToEither()
        select unit;

    private async Task<GenesInfo> RemoveMergedGene(string geneSetPath, string geneHash)
    {
        var genesInfo = await ReadGenesInfo(geneSetPath);
        genesInfo.MergedGenes = genesInfo.MergedGenes?.Where(h => h != geneHash).ToArray();
        await WriteGenesInfo(geneSetPath, genesInfo);
        return genesInfo;
    }

    private async Task<GenesInfo> ReadGenesInfo(string geneSetPath)
    {
        if (!fileSystem.FileExists(Path.Combine(geneSetPath, GenesFileName)))
            return new GenesInfo { MergedGenes = [] };

        var json = await fileSystem.ReadAllTextAsync(Path.Combine(geneSetPath, GenesFileName));
        return JsonSerializer.Deserialize<GenesInfo>(json);
    }

    private async Task WriteGenesInfo(string geneSetPath, GenesInfo genesInfo)
    {
        var json = JsonSerializer.Serialize(genesInfo);
        await fileSystem.WriteAllTextAsync(Path.Combine(geneSetPath, GenesFileName), json);
    }

    private class GenesInfo
    {
        public string[]? MergedGenes { get; set; }
    }

    private static string GetGenesInfoPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId) =>
        Path.Combine(
            GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId),
            GenesFileName);

    private static string GetGeneTempPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId,
        string geneHash) =>
        Path.Combine(
            GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId),
            geneHash);
}
