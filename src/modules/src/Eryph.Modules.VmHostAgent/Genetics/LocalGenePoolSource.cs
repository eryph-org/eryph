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
            return new GenesInfo { MergedGenes = Array.Empty<string>() };

        var json = fileSystem.ReadText(Path.Combine(genesetInfo.LocalPath, "genes.json"));
        var genes = JsonSerializer.Deserialize<GenesInfo>(json);

        return genes?.MergedGenes == null ? new GenesInfo { MergedGenes = Array.Empty<string>() } : genes;
    }

    public EitherAsync<Error, GeneInfo> RetrieveGene(GeneSetInfo genesetInfo, 
        GeneIdentifier geneIdentifier, string geneHash, CancellationToken cancel)
    {

        return ParseGeneHash(geneHash).Bind(parsedHashed =>
        {
            var (hashAlgName, hash) = parsedHashed;

            var mergedGenesInfo = ReadGenesInfo(genesetInfo);
            mergedGenesInfo.MergedGenes ??= Array.Empty<string>();

            if (mergedGenesInfo.MergedGenes.Contains(geneHash))
            {
                return new GeneInfo(geneIdentifier, hash, hashAlgName, null,
                    Array.Empty<GenePartDownloadUri>(),DateTimeOffset.MinValue, 
                    null, true);
            }


            var genePath = Path.Combine(genesetInfo.LocalPath, hash);
            var cachedGeneManifestFile = Path.Combine(genePath, "gene.json");

            if (!fileSystem.FileExists(cachedGeneManifestFile))
                return Error.New($"{geneIdentifier} not available on local gene pool");


            return Prelude.Try(() =>
            {
                var hashAlg = CreateHashAlgorithm(hashAlgName);

                var manifestJsonData = fileSystem.ReadText(cachedGeneManifestFile);
                var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));

                cancel.ThrowIfCancellationRequested();

                if (hashString == hash)
                {

                    var manifestData = JsonSerializer.Deserialize<GeneManifestData>(manifestJsonData);
                    return new GeneInfo(geneIdentifier, hash, hashAlgName,
                        manifestData,
                        Array.Empty<GenePartDownloadUri>(), DateTimeOffset.MinValue,
                        genePath, false);
                }


                fileSystem.FileDelete(cachedGeneManifestFile);

                throw new VerificationException($"Failed to verify hash of {geneIdentifier}");


            }).ToEither(Error.New);

        }).ToAsync();

    }

    public EitherAsync<Error, long> RetrieveGenePart(GeneInfo gene, string genePart,
        long availableSize, long totalSize,
        Func<string, int, Task<Unit>> reportProgress, Stopwatch stopwatch, CancellationToken cancel)
    {
        return ParseGenePartHash(genePart).BindAsync(async parsedGenePartName =>
        {
            var (hashAlgName, partHash) = parsedGenePartName;
            var messageName = $"{gene}/{partHash[..12]}";

            var cachedGenePartFile = Path.Combine(gene.LocalPath, $"{partHash}.part");

            if (!fileSystem.FileExists(cachedGenePartFile))
                return Error.New($"gene part '{messageName}' not available on local store");


            var hashAlg = CreateHashAlgorithm(hashAlgName);
            await using (var dataStream = File.OpenRead(cachedGenePartFile))
            {
                await hashAlg.ComputeHashAsync(dataStream, cancel);
            }

            var hashString = GetHashString(hashAlg.Hash);


            if (hashString != partHash)
            {
                return Error.New($"Failed to verify hash of gene part '{messageName}'");
            }

            return await Prelude.RightAsync<Error, long>(fileSystem.GetFileSize(cachedGenePartFile));
        }).ToAsync();
    }

    public string PoolName => poolName;

    public EitherAsync<Error, Unit> MergeGenes(GeneInfo geneInfo, GeneSetInfo genesetInfo,
        Func<string, int, Task<Unit>> reportProgress, CancellationToken cancel)
    {
        var mergedGenesInfo = ReadGenesInfo(genesetInfo);

        var mergedGenes = mergedGenesInfo.MergedGenes ?? Array.Empty<string>();

        if (mergedGenes.Contains($"{geneInfo.HashAlg}:{geneInfo.Hash}") || geneInfo.LocalPath == null)
            return Unit.Default;

        return Prelude.TryAsync(async () =>
        {

            var parts = geneInfo.MetaData?.Parts ?? Array.Empty<string>();

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
                    multiStream, genesetInfo.LocalPath, cancel);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }

            await using var genesInfoStream = File.Open(Path.Combine(genesetInfo.LocalPath, "genes.json"),
                FileMode.Create);

            mergedGenesInfo.MergedGenes = mergedGenesInfo.MergedGenes
                .Append(new[] { $"{geneInfo.HashAlg}:{geneInfo.Hash}" }).ToArray();
            await JsonSerializer.SerializeAsync(genesInfoStream, mergedGenesInfo, cancellationToken: cancel);

            fileSystem.DirectoryDelete(geneInfo.LocalPath);

            return Unit.Default;

        }).ToEither(ex => Error.New(ex));

    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        string path,
        GeneSetIdentifier genesetIdentifier,
        CancellationToken cancel) =>
        Prelude.TryAsync(async () =>
        {
            var genesetPath = BuildGeneSetPath(genesetIdentifier, path);
            if (!File.Exists(Path.Combine(genesetPath, "geneset-tag.json")))
                return await Prelude.LeftAsync<Error, GeneSetInfo>(Error.New(
                    $"Geneset '{genesetIdentifier.Value}' not found in local gene pool.")).ToEither();

            await using var manifestStream = File.OpenRead(Path.Combine(genesetPath, "geneset-tag.json"));
            var manifest =
                await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancel);

            return await Prelude
                .RightAsync<Error, GeneSetInfo>(new GeneSetInfo(genesetIdentifier, genesetPath, manifest, Array.Empty<GetGeneDownloadResponse>()))
                .ToEither();

        })
        .ToEither(ex => Error.New(ex))
        .Bind(e => e.ToAsync());

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(string path, GeneSetInfo genesetInfo, CancellationToken cancel)
    {
        return Prelude.TryAsync(async () =>
        {
            var genesetPath = BuildGeneSetPath(genesetInfo.Id, path, true);

            await using var manifestStream = fileSystem.OpenWrite(Path.Combine(genesetPath, "geneset-tag.json"));
            await JsonSerializer.SerializeAsync(manifestStream, genesetInfo.MetaData, cancellationToken: cancel);
            return new GeneSetInfo(genesetInfo.Id, genesetPath, genesetInfo.MetaData,
                genesetInfo.GeneDownloadInfo);

        }).ToEither(ex => Error.New(ex));


    }

    public EitherAsync<Error, GeneInfo> CacheGene(GeneInfo geneInfo, GeneSetInfo imageInfo, CancellationToken cancel)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return Prelude.TryAsync(async () =>
        {
            var genePath = Path.Combine(imageInfo.LocalPath, geneInfo.Hash);
            fileSystem.EnsureDirectoryExists(genePath);

            await using var manifestStream = fileSystem.OpenWrite(Path.Combine(genePath, "gene.json"));
            await JsonSerializer.SerializeAsync(manifestStream, geneInfo.MetaData, cancellationToken: cancel);
            return new GeneInfo(geneInfo.GeneId, geneInfo.Hash, geneInfo.HashAlg, geneInfo.MetaData,
                geneInfo.DownloadUris, geneInfo.DownloadExpires,
                genePath,
                false);

        }).ToEither(ex => Error.New(ex));


    }

    private class GenesInfo
    {
        public string[]? MergedGenes { get; set; }
    }
}