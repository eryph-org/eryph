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
using Eryph.GenePool.Model.Responses;
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
    string poolName)
    : GenePoolBase, ILocalGenePool
{
    public EitherAsync<Error, GeneInfo> RetrieveGene(
        GeneSetInfo geneSetInfo,
        GeneIdentifier geneIdentifier,
        string geneHash,
        CancellationToken cancel) =>
        from parsedHash in ParseGeneHash(geneHash).ToAsync()
        from genesInfo in TryAsync(() => ReadGenesInfo(geneSetInfo)).ToEither()
        from geneInfo in genesInfo.MergedGenes.ToSeq().Find(h => h == geneHash).Match(
            Some: h => new GeneInfo(geneIdentifier, h, parsedHash.HashAlg, null,
                [], DateTimeOffset.MinValue, null, true),
            None: () =>
                from _ in guard(notEmpty(geneSetInfo.LocalPath),
                        Error.New("The local path was not provided with the gene set."))
                    .ToEitherAsync()
                let genePath = Path.Combine(geneSetInfo.LocalPath, parsedHash.Hash)
                let cachedGeneManifestPath = Path.Combine(genePath, "gene.json")
                from manifestExists in Try(() => fileSystem.FileExists(cachedGeneManifestPath))
                    .ToEitherAsync()
                from _2 in guard(manifestExists,
                    Error.New($"The gene '{geneIdentifier}' is not available on the local gene pool."))
                from manifestJson in Try(() =>
                {
                    using var hashAlg = CreateHashAlgorithm(parsedHash.HashAlg);

                    var manifestJsonData = fileSystem.ReadText(cachedGeneManifestPath);
                    var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));
                    if (hashString == parsedHash.Hash)
                        return manifestJsonData;
                    
                    fileSystem.FileDelete(cachedGeneManifestPath);
                    throw new VerificationException($"Failed to verify the hash of the manifest of '{geneIdentifier}'");
                }).ToEitherAsync()
                from manifestData in Try(() => JsonSerializer.Deserialize<GeneManifestData>(manifestJson))
                    .ToEitherAsync()
                select new GeneInfo(geneIdentifier, parsedHash.Hash, parsedHash.HashAlg,
                    manifestData, [], DateTimeOffset.MinValue, genePath, false))
        select geneInfo;

    public EitherAsync<Error, long> RetrieveGenePart(
        GeneInfo geneInfo,
        string genePartHash,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel) =>
        from parsedHash in ParseGenePartHash(genePartHash).ToAsync()
        from _1 in guard(notEmpty(geneInfo.LocalPath),
                Error.New("The local path was not provided with the gene."))
            .ToEitherAsync()
        let cachedGenePartPath = Path.Combine(geneInfo.LocalPath!, $"{parsedHash.Hash}.part")
        from partExists in Try(() => fileSystem.FileExists(cachedGenePartPath))
            .ToEitherAsync()
        from _2 in guard(partExists,
            Error.New($"The gene part '{geneInfo}/{parsedHash.Hash[..12]}' is not available on the local gene pool."))
        from isHashValid in TryAsync(async () =>
        {
            using var hashAlg = CreateHashAlgorithm(parsedHash.HashAlg);
            await using var dataStream = File.OpenRead(cachedGenePartPath);

            await hashAlg.ComputeHashAsync(dataStream, cancel);
            var hashString = GetHashString(hashAlg.Hash);

            return hashString == parsedHash.Hash;
        }).ToEither()
        from _3 in guard(isHashValid,
            Error.New($"Failed to verify the hash of the gene part '{geneInfo}/{parsedHash.Hash[..12]}'."))
        from partSize in Try(() => fileSystem.GetFileSize(cachedGenePartPath))
            .ToEitherAsync()
        select partSize;

    public string PoolName => poolName;

    public EitherAsync<Error, Unit> MergeGenes(GeneInfo geneInfo, GeneSetInfo geneSetInfo,
        Func<string, int, Task<Unit>> reportProgress, CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var mergedGenesInfo = await ReadGenesInfo(geneSetInfo);

            var mergedGenes = mergedGenesInfo.MergedGenes ?? [];

            if (mergedGenes.Contains($"{geneInfo.HashAlg}:{geneInfo.Hash}") || geneInfo.LocalPath == null)
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
                var decompression = new GeneDecompression(fileSystem,log, reportProgress, geneInfo );
                await decompression.Decompress(geneInfo.MetaData,
                    multiStream, geneSetInfo.LocalPath, cancel);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }

            await using var genesInfoStream = File.Open(Path.Combine(geneSetInfo.LocalPath, "genes.json"),
                FileMode.Create);

            mergedGenesInfo.MergedGenes = mergedGenesInfo.MergedGenes
                .Append([$"{geneInfo.HashAlg}:{geneInfo.Hash}"])
                .ToArray();
            await JsonSerializer.SerializeAsync(genesInfoStream, mergedGenesInfo, cancellationToken: cancel);

            fileSystem.DeleteDirectory(geneInfo.LocalPath);

            return unit;
        }).ToEither();
    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        string path,
        GeneSetIdentifier geneSetIdentifier,
        CancellationToken cancel) =>
        TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(path, geneSetIdentifier);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(path, geneSetIdentifier);
            if (!fileSystem.FileExists(geneSetManifestPath))
                return await Prelude.LeftAsync<Error, GeneSetInfo>(Error.New(
                    $"Geneset '{geneSetIdentifier.Value}' not found in local gene pool.")).ToEither();

            await using var manifestStream = File.OpenRead(geneSetManifestPath);
            var manifest =
                await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancel);

            return Right<Error, GeneSetInfo>(new GeneSetInfo(geneSetIdentifier, geneSetPath, manifest, []));

        })
        .ToEither()
        .Bind(e => e.ToAsync());

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(string path, GeneSetInfo geneSetInfo, CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(path, geneSetInfo.Id);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(path, geneSetInfo.Id);
            fileSystem.EnsureDirectoryExists(geneSetPath);
            
            await using var manifestStream = fileSystem.OpenWrite(geneSetManifestPath);
            await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.MetaData, cancellationToken: cancel);
            return new GeneSetInfo(geneSetInfo.Id, geneSetPath, geneSetInfo.MetaData,
                geneSetInfo.GeneDownloadInfo);

        }).ToEither();
    }

    public EitherAsync<Error, GeneInfo> CacheGene(GeneInfo geneInfo, GeneSetInfo geneSetInfo, CancellationToken cancel)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return TryAsync(async () =>
        {
            var genePath = Path.Combine(geneSetInfo.LocalPath, geneInfo.Hash);
            fileSystem.EnsureDirectoryExists(genePath);

            await using var manifestStream = fileSystem.OpenWrite(Path.Combine(genePath, "gene.json"));
            await JsonSerializer.SerializeAsync(manifestStream, geneInfo.MetaData, cancellationToken: cancel);
            return new GeneInfo(geneInfo.GeneId, geneInfo.Hash, geneInfo.HashAlg, geneInfo.MetaData,
                geneInfo.DownloadUris, geneInfo.DownloadExpires,
                genePath,
                false);

        }).ToEither();
    }

    public EitherAsync<Error, GeneSetInfo> GetCachedGeneSet(
        string genePoolPath,
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
        string genePoolPath,
        GeneType geneType,
        GeneIdentifier geneId) =>
        from _ in RightAsync<Error, Unit>(unit)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, geneId)
        from geneExists in Try(() => fileSystem.FileExists(genePath))
            .ToEitherAsync()
        from fileSize in geneExists
            ? Try(() => fileSystem.GetFileSize(genePath)).ToEitherAsync().Map(Optional)
            : RightAsync<Error, Option<long>>(None)
        select fileSize;

    public EitherAsync<Error, Unit> RemoveCachedGene(
        string genePoolPath,
        GeneType geneType,
        GeneIdentifier geneId) =>
        from _ in TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneId.GeneSet);
            var manifestPath = GenePoolPaths.GetGeneSetManifestPath(geneSetPath, geneId.GeneSet);
            if (!fileSystem.FileExists(manifestPath))
                return unit;

            var manifestJson = await fileSystem.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<GenesetTagManifestData>(manifestJson);

            var geneHash = GeneSetManifestUtils.FindGeneHash(
                manifest, geneType, geneId.GeneName);
            if (geneHash.IsNone)
                return unit;

            var genes = await RemoveMergedGene(geneSetPath, geneHash.ValueUnsafe());
            
            var genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, geneId);
            fileSystem.DeleteFile(genePath);

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

    private Task<GenesInfo> ReadGenesInfo(GeneSetInfo genesetInfo)
        => ReadGenesInfo(genesetInfo.LocalPath);

    private async Task<GenesInfo> ReadGenesInfo(string geneSetPath)
    {
        if (!fileSystem.FileExists(Path.Combine(geneSetPath, "genes.json")))
            return new GenesInfo { MergedGenes = [] };

        var json = await fileSystem.ReadAllTextAsync(Path.Combine(geneSetPath, "genes.json"));
        return JsonSerializer.Deserialize<GenesInfo>(json);
    }

    private async Task WriteGenesInfo(string geneSetPath, GenesInfo genesInfo)
    {
        var json = JsonSerializer.Serialize(genesInfo);
        await fileSystem.WriteAllTextAsync(Path.Combine(geneSetPath, "genes.json"), json);
    }

    private class GenesInfo
    {
        public string[]? MergedGenes { get; set; }
    }
}
