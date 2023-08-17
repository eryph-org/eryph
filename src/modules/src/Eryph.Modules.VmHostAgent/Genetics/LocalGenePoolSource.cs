using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Genetics;

[UsedImplicitly]
internal class LocalGenePoolSource : GenePoolBase, ILocalGenePool
{
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger _log;

    public LocalGenePoolSource(IFileSystemService fileSystem, ILogger log)
    {
        _fileSystem = fileSystem;
        _log = log;
    }

    private string BuildGeneSetPath(GeneSetIdentifier genesetIdentifier, string basePath, bool shouldExists = false)
    {
        var orgDirectory = Path.Combine(basePath, genesetIdentifier.Organization);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(orgDirectory);
        var poolBaseDirectory = Path.Combine(orgDirectory, genesetIdentifier.GeneSet);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(poolBaseDirectory);
        var imageTagDirectory = Path.Combine(poolBaseDirectory, genesetIdentifier.Tag);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(imageTagDirectory);

        return imageTagDirectory;
    }

    private GenesInfo ReadGenesInfo(GeneSetInfo genesetInfo)
    {
        if (!_fileSystem.FileExists(Path.Combine(genesetInfo.LocalPath, "genes.json")))
            return new GenesInfo { MergedGenes = Array.Empty<string>() };

        var json = _fileSystem.ReadText(Path.Combine(genesetInfo.LocalPath, "genes.json"));
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
                return new GeneInfo(geneIdentifier, hash, hashAlgName, null, null, true);
            }


            var genePath = Path.Combine(genesetInfo.LocalPath, hash);
            var cachedGeneManifestFile = Path.Combine(genePath, "gene.json");

            if (!_fileSystem.FileExists(cachedGeneManifestFile))
                return Error.New($"{geneIdentifier} not available on local gene pool");


            return Prelude.Try(() =>
            {
                var hashAlg = CreateHashAlgorithm(hashAlgName);

                var manifestJsonData = _fileSystem.ReadText(cachedGeneManifestFile);
                var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));

                cancel.ThrowIfCancellationRequested();

                if (hashString == hash)
                {

                    var manifestData = JsonSerializer.Deserialize<GeneManifestData>(manifestJsonData);
                    return new GeneInfo(geneIdentifier, hash, hashAlgName,
                        manifestData, genePath, false);
                }


                _fileSystem.FileDelete(cachedGeneManifestFile);

                throw new VerificationException($"Failed to verify hash of {geneIdentifier}");


            }).ToEither(Error.New);

        }).ToAsync();

    }

    public EitherAsync<Error, long> RetrieveGenePart(GeneInfo gene, string genePart,
        long availableSize, long totalSize,
        Func<string, Task<Unit>> reportProgress, Stopwatch stopwatch, CancellationToken cancel)
    {
        return ParseGenePartHash(genePart).BindAsync(async parsedGenePartName =>
        {
            var (hashAlgName, partHash) = parsedGenePartName;
            var messageName = $"{gene}/{partHash[..12]}";

            var cachedGenePartFile = Path.Combine(gene.LocalPath, $"{partHash}.part");

            if (!_fileSystem.FileExists(cachedGenePartFile))
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

            return await Prelude.RightAsync<Error, long>(_fileSystem.GetFileSize(cachedGenePartFile));
        }).ToAsync();
    }

    public string PoolName { get; set; }


    public EitherAsync<Error, Unit> MergeGenes(GeneInfo geneInfo, GeneSetInfo genesetInfo,
        Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
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
                return _fileSystem.OpenRead(path);
            }).ToArray();
            try
            {
                await using var multiStream = new MultiStream(streams);
                var decompression = new GeneDecompression(_fileSystem,_log, reportProgress, geneInfo );
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

            _fileSystem.DirectoryDelete(geneInfo.LocalPath);

            return Unit.Default;

        }).ToEither(ex => Error.New(ex));

    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier genesetIdentifier, CancellationToken cancel)
    {
        return ProvideGeneSet(path, genesetIdentifier, false, cancel);
    }


    public EitherAsync<Error, GeneSetInfo> ProvideFallbackGeneSet(string path, GeneSetIdentifier genesetIdentifier, CancellationToken cancel)
    {
        return ProvideGeneSet(path, genesetIdentifier, true, cancel);
    }


    private EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier genesetIdentifier,
        bool fallbackMode, CancellationToken cancel)
    {
        if (!fallbackMode && genesetIdentifier.Tag == "latest")
            return Error.New("latest geneset version will be look up first on remote sources.");

        return Prelude.TryAsync(async () =>
            {
                var genesetPath = BuildGeneSetPath(genesetIdentifier, path);
                if (!File.Exists(Path.Combine(genesetPath, "geneset.json")))
                    return await Prelude.LeftAsync<Error, GeneSetInfo>(Error.New(
                        $"Geneset '{genesetIdentifier.Name}' not found in local gene pool.")).ToEither();

                await using var manifestStream = File.OpenRead(Path.Combine(genesetPath, "geneset.json"));
                var manifest =
                    await JsonSerializer.DeserializeAsync<GeneSetManifestData>(manifestStream,
                        cancellationToken: cancel);

                return await Prelude
                    .RightAsync<Error, GeneSetInfo>(new GeneSetInfo(genesetIdentifier, genesetPath, manifest))
                    .ToEither();

            })
            .ToEither(ex => Error.New(ex))
            .Bind(e => e.ToAsync());

    }

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(string path, GeneSetInfo imageInfo, CancellationToken cancel)
    {
        return Prelude.TryAsync(async () =>
        {
            var genesetPath = BuildGeneSetPath(imageInfo.Id, path, true);

            await using var manifestStream = _fileSystem.OpenWrite(Path.Combine(genesetPath, "geneset.json"));
            await JsonSerializer.SerializeAsync(manifestStream, imageInfo.MetaData, cancellationToken: cancel);
            return new GeneSetInfo(imageInfo.Id, genesetPath, imageInfo.MetaData);

        }).ToEither(ex => Error.New(ex));


    }

    public EitherAsync<Error, GeneInfo> CacheGene(GeneInfo geneInfo, GeneSetInfo imageInfo, CancellationToken cancel)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return Prelude.TryAsync(async () =>
        {
            var genePath = Path.Combine(imageInfo.LocalPath, geneInfo.Hash);
            _fileSystem.EnsureDirectoryExists(genePath);

            await using var manifestStream = _fileSystem.OpenWrite(Path.Combine(genePath, "gene.json"));
            await JsonSerializer.SerializeAsync(manifestStream, geneInfo.MetaData, cancellationToken: cancel);
            return new GeneInfo(geneInfo.GeneId, geneInfo.Hash, geneInfo.HashAlg, geneInfo.MetaData,
                genePath, false);

        }).ToEither(ex => Error.New(ex));


    }

    private class GenesInfo
    {
        public string[]? MergedGenes { get; set; }
    }
}