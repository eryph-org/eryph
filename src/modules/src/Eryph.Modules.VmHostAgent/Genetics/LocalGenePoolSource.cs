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
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

[UsedImplicitly]
internal class LocalGenePoolSource(
    IFileSystemService fileSystem,
    ILogger log,
    string poolName)
    : GenePoolBase, ILocalGenePool
{
    private string BuildGeneSetPath(GeneSetIdentifier genesetIdentifier, string basePath, bool shouldExists = false)
    {
        var orgDirectory = Path.Combine(basePath, genesetIdentifier.Organization.Value);
        if (shouldExists) fileSystem.EnsureDirectoryExists(orgDirectory);
        var poolBaseDirectory = Path.Combine(orgDirectory, genesetIdentifier.GeneSet.Value);
        if (shouldExists) fileSystem.EnsureDirectoryExists(poolBaseDirectory);
        var imageTagDirectory = Path.Combine(poolBaseDirectory, genesetIdentifier.Tag.Value);
        if (shouldExists) fileSystem.EnsureDirectoryExists(imageTagDirectory);

        return imageTagDirectory;
    }

    private GenesInfo ReadGenesInfo(GeneSetInfo genesetInfo)
    {
        if (!fileSystem.FileExists(Path.Combine(genesetInfo.LocalPath, "genes.json")))
            return new GenesInfo { MergedGenes = [] };

        var json = fileSystem.ReadText(Path.Combine(genesetInfo.LocalPath, "genes.json"));
        var genes = JsonSerializer.Deserialize<GenesInfo>(json);

        return genes?.MergedGenes == null ? new GenesInfo { MergedGenes = [] } : genes;
    }

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        GeneSetInfo geneSetInfo,
        GeneIdentifier geneIdentifier,
        string geneHash,
        CancellationToken cancel) =>
        from parsedHash in ParseGeneHash(geneHash).ToAsync()
        from genesInfo in Try(() => ReadGenesInfo(geneSetInfo)).ToEitherAsync()
        from geneInfo in genesInfo.MergedGenes.ToSeq().Find(h => h == parsedHash.Hash).Match(
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
        let cachedGenePartFile = Path.Combine(geneInfo.LocalPath!, $"{parsedHash.Hash}.part")
        from partExists in Try(() => fileSystem.FileExists(cachedGenePartFile))
            .ToEitherAsync()
        from _2 in guard(partExists,
            Error.New($"The gene part '{geneInfo}/{parsedHash.Hash[..12]}' is not available on the local gene pool."))
        from isHashValid in TryAsync(async () =>
        {
            using var hashAlg = CreateHashAlgorithm(parsedHash.HashAlg);
            await using var dataStream = File.OpenRead(cachedGenePartFile);

            await hashAlg.ComputeHashAsync(dataStream, cancel);
            var hashString = GetHashString(hashAlg.Hash);

            return hashString == parsedHash.Hash;
        }).ToEither()
        from _3 in guard(isHashValid,
            Error.New($"Failed to verify the hash of the gene part '{geneInfo}/{parsedHash.Hash[..12]}'."))
        from partSize in Try(() => fileSystem.GetFileSize(cachedGenePartFile))
            .ToEitherAsync()
        select partSize;

    public string PoolName => poolName;

    public EitherAsync<Error, Unit> MergeGenes(GeneInfo geneInfo, GeneSetInfo geneSetInfo,
        Func<string, int, Task<Unit>> reportProgress, CancellationToken cancel)
    {
        var mergedGenesInfo = ReadGenesInfo(geneSetInfo);

        var mergedGenes = mergedGenesInfo.MergedGenes ?? [];

        if (mergedGenes.Contains($"{geneInfo.HashAlg}:{geneInfo.Hash}") || geneInfo.LocalPath == null)
            return Unit.Default;

        return TryAsync(async () =>
        {

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

            fileSystem.DirectoryDelete(geneInfo.LocalPath);

            return unit;
        }).ToEither();
    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        string path,
        GeneSetIdentifier geneSetIdentifier,
        CancellationToken cancel) =>
        TryAsync(async () =>
        {
            var genesetPath = BuildGeneSetPath(geneSetIdentifier, path);
            if (!File.Exists(Path.Combine(genesetPath, "geneset-tag.json")))
                return await Prelude.LeftAsync<Error, GeneSetInfo>(Error.New(
                    $"Geneset '{geneSetIdentifier.Value}' not found in local gene pool.")).ToEither();

            await using var manifestStream = File.OpenRead(Path.Combine(genesetPath, "geneset-tag.json"));
            var manifest =
                await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancel);

            return Right<Error, GeneSetInfo>(new GeneSetInfo(geneSetIdentifier, genesetPath, manifest, []));

        })
        .ToEither()
        .Bind(e => e.ToAsync());

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(string path, GeneSetInfo geneSetInfo, CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var genesetPath = BuildGeneSetPath(geneSetInfo.Id, path, true);

            await using var manifestStream = fileSystem.OpenWrite(Path.Combine(genesetPath, "geneset-tag.json"));
            await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.MetaData, cancellationToken: cancel);
            return new GeneSetInfo(geneSetInfo.Id, genesetPath, geneSetInfo.MetaData,
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

    private class GenesInfo
    {
        public string[]? MergedGenes { get; set; }
    }
}
