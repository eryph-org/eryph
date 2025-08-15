using System;
using System.Buffers;
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
using Eryph.Core.Sys;
using Eryph.GenePool.Compression;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Pipes;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenePool.Genetics;

[UsedImplicitly]
internal class LocalGenePoolSource(
    IFileSystemService fileSystem,
    ILogger log,
    string poolName,
    string genePoolPath)
    : GenePoolBase, ILocalGenePool
{
    
    // TODO add file system locking with DistributedLock

    // genes.json contains the hashes with the algorithm identifier, e.g. sha256:...
    private const string GenesFileName = "genes.json";

    public EitherAsync<Error, bool> HasGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from mergedGenes in GetMergedGenes(uniqueGeneId.Id.GeneSet)
        let isGeneMerged = mergedGenes.Contains(geneHash)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from fileExists in Try(() => fileSystem.FileExists(genePath))
            .ToEitherAsync()
        select isGeneMerged && fileExists;

    public EitherAsync<Error, Option<GenePartsInfo>> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<long, long, Task<Unit>> reportProgress,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        from manifest in ReadTempGeneManifest(uniqueGeneId, geneHash, cancellationToken)
        from geneParts in manifest
            .Map(m => GetDownloadedGeneParts(uniqueGeneId, geneHash, m, reportProgress, cancellationToken))
            .Sequence()
        select geneParts;

    private EitherAsync<Error, GenePartsInfo> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GeneManifestData manifest,
        Func<long, long, Task<Unit>> reportProgress,
        CancellationToken cancellationToken) =>
        from geneParts in GeneManifestUtils.GetParts(manifest).ToAsync()
        from totalBytes in Optional(manifest.Size).ToEitherAsync(
            Error.New($"The gene manifest of {uniqueGeneId} ({geneHash.Hash}) does not contain a size."))
        from allPartsPresent in geneParts
            .Map(gp => IsGenePartPresent(uniqueGeneId, geneHash, gp))
            .SequenceSerial()
        from existingParts in geneParts.Fold<EitherAsync<Error, Seq<(GenePartHash Part, long Size)>>> (
            Seq<(GenePartHash Part, long Size)>(),
            (state, part) => from partInfos in state
                             from partInfo in GetDownloadedGenePart(uniqueGeneId, geneHash, part, CancellationToken.None)
                             let result = partInfos.Concat(partInfo.ToSeq())
                             let availableBytes = result.Sum(p => p.Size)
                             from _ in TryAsync(() => reportProgress(availableBytes, totalBytes)).ToEither()
                             select result)
        select new GenePartsInfo(uniqueGeneId, geneHash, geneParts, existingParts.ToHashMap());


    private EitherAsync<Error, Option<(GenePartHash Part, long Size)>> GetDownloadedGenePart(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartHash genePartHash,
        CancellationToken cancellationToken) =>
        from path in GetGenePartPath(uniqueGeneId, geneHash, genePartHash)
        from size in TryAsync(async () =>
        {
            if (!fileSystem.FileExists(path))
                return None;

            using var hashAlgorithm = genePartHash.CreateAlgorithm();
            await using var dataStream = fileSystem.OpenRead(path);
            await hashAlgorithm.ComputeHashAsync(dataStream, cancellationToken);

            var actualGenePartHash = hashAlgorithm.ToGenePartHash();

            if (actualGenePartHash.Hash == genePartHash.Hash)
                return Some(dataStream.Length);

            fileSystem.FileDelete(path);
            return None;
        }).ToEither()
        select size.Map(s => (genePartHash, s));


    private EitherAsync<Error, bool> IsGenePartPresent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartHash genePartHash) =>
        from path in GetGenePartPath(uniqueGeneId, geneHash, genePartHash)
        from isPresent in Try(() => fileSystem.FileExists(path)).ToEitherAsync()
        select isPresent;

    private EitherAsync<Error, Option<GeneManifestData>> ReadTempGeneManifest(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        from geneTempPath in GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, geneHash)
        let manifestPath = Path.Combine(geneTempPath, "gene.json")
        from manifest in TryAsync(async () =>
        {
            if (!fileSystem.FileExists(manifestPath))
                return None;

            var json = await fileSystem.ReadAllTextAsync(manifestPath, cancellationToken);
            return Some(JsonSerializer.Deserialize<GeneManifestData>(json, GeneModelDefaults.SerializerOptions));
        }).ToEither()
        let actualGeneHash = manifest.Map(GeneManifestUtils.ComputeHash)
        // TODO error or return nothing?
        from _2 in guard(
            actualGeneHash.IsSome && actualGeneHash != geneHash,
            Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash.Hash}) in the local gene pool is corrupted."))
        select manifest;

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancel) =>
        from parsedGeneHash in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from genesInfo in TryAsync(() => ReadGenesInfo(geneSetPath)).ToEither()
        from geneInfo in genesInfo.MergedGenes.ToSeq().Find(h => h == geneHash.Value).Match(
            Some: _ => new GeneInfo(uniqueGeneId, geneHash, null, [], DateTimeOffset.MinValue, true),
            None: () =>
                from _ in RightAsync<Error, Unit>(unit)
                from geneTempPath in GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, geneHash)
                let cachedGeneManifestPath = Path.Combine(geneTempPath, "gene.json")
                from manifestExists in Try(() => fileSystem.FileExists(cachedGeneManifestPath))
                    .ToEitherAsync()
                from _2 in guard(manifestExists,
                    Error.New($"The gene '{uniqueGeneId}' is not available on the local gene pool."))
                from manifestJson in TryAsync(async () =>
                {
                    using var hashAlg = CreateHashAlgorithm(geneHash.Algorithm);

                    var manifestJsonData = await fileSystem.ReadAllTextAsync(cachedGeneManifestPath, cancel);
                    var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));
                    if (hashString == geneHash.Hash)
                        return manifestJsonData;
                    
                    fileSystem.FileDelete(cachedGeneManifestPath);
                    throw new VerificationException($"Failed to verify the hash of the manifest of '{uniqueGeneId}'");
                }).ToEither()
                from manifestData in Try(() => JsonSerializer.Deserialize<GeneManifestData>(manifestJson))
                    .ToEitherAsync()
                select new GeneInfo(uniqueGeneId, geneHash, manifestData, [], DateTimeOffset.MinValue, false))
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
        Func<long, long, Task<Unit>> reportProgress,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneInfo.Id.Id.GeneSet)
        from isGeneMerged in TryAsync(async () =>
        {
            var mergedGenesInfo = await ReadGenesInfo(geneSetPath);
            return mergedGenesInfo.MergedGenes.ToSeq().Contains(geneInfo.Hash.Value);
        }).ToEither()
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneInfo.Id)
        let parts = (geneInfo.Manifest?.Parts).ToSeq()
        from totalSize in Optional(geneInfo.Manifest?.OriginalSize)
            .ToEitherAsync(Error.New($"The gene manifest of {geneInfo.Id} ({geneInfo.Hash.Hash}) does not contain a size."))
        from _2 in parts.IsEmpty || isGeneMerged
            ? RightAsync<Error, Unit>(unit)
            : from partPaths in parts
                .Map(part => from partHash in GenePartHash.NewEither(part).ToAsync()
                             from path in GetGenePartPath(geneInfo.Id, geneInfo.Hash, partHash)
                             select path)
                .SequenceSerial()
              from _1 in TryAsync(async () =>
              {
                  var streams = partPaths
                      .Map(fileSystem.OpenRead)
                      .ToList();
              
                  try
                  {
                      await using var multiStream = new MultiStream(streams);
                      await using var decompressionStream = CompressionStreamFactory.CreateDecompressionStream(
                          multiStream, geneInfo.Manifest!.Format!);

                      await using var fileStream = fileSystem.OpenWrite(genePath);
                      await using var progressStream = new ProgressStream(
                          fileStream,
                          TimeSpan.FromSeconds(10),
                          // TODO Fix cancellation token
                          async (writtenBytes, _) => await reportProgress(writtenBytes, totalSize));

                      await decompressionStream.CopyToAsync(progressStream, cancellationToken);
                  }
                  finally
                  {
                      foreach (var stream in streams)
                      {
                          await stream.DisposeAsync();
                      }
                  }
              
                  return unit;
              }).ToEither()
              from _2 in AddMergedGene(geneSetPath, geneInfo.Hash)
              from geneTempPath in GetGeneTempPath(genePoolPath, geneInfo.Id.Id.GeneSet, geneInfo.Hash)
              from _3 in Try(() =>
              {
                  fileSystem.DeleteDirectory(geneTempPath);
                  return unit;
              }).ToEitherAsync()
              select unit
        select unit;

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancel) =>
        TryAsync(async () =>
        {
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId);
            if (!fileSystem.FileExists(geneSetManifestPath))
                return await LeftAsync<Error, GeneSetInfo>(Error.New(
                    $"Geneset '{geneSetId.Value}' not found in local gene pool.")).ToEither();

            await using var manifestStream = File.OpenRead(geneSetManifestPath);
            var manifest =
                await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(manifestStream,
                    cancellationToken: cancel);

            return Right<Error, GeneSetInfo>(new GeneSetInfo(geneSetId, manifest, []));

        })
        .ToEither()
        .Bind(e => e.ToAsync());

    public EitherAsync<Error, GeneSetInfo> CacheGeneSet(
        GeneSetInfo geneSetInfo,
        CancellationToken cancellationToken) =>
        from result in TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetInfo.Id);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetInfo.Id);
            fileSystem.EnsureDirectoryExists(geneSetPath);

            await using var manifestStream = fileSystem.OpenWrite(geneSetManifestPath);
            await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.Manifest,
                cancellationToken: cancellationToken);
            return geneSetInfo;

        }).ToEither()
        let catletReference = GeneHash.NewValidation(geneSetInfo.Manifest.CatletGene).ToOption()
            .Map(geneHash => new GeneWithHash(
                    new UniqueGeneIdentifier(
                GeneType.Catlet,
                new GeneIdentifier(geneSetInfo.Id, GeneName.New("catlet")),
                Architecture.New(EryphConstants.AnyArchitecture)),
                geneHash))
            .ToSeq()
        let fodderReferences = geneSetInfo.Manifest.FodderGenes.ToSeq()
            .Map(gene => from geneHash in GeneHash.NewOption(gene.Hash)
                         from geneName in GeneName.NewOption(gene.Name)
                from architecture in Architecture.NewOption(gene.Architecture)
                let geneId = new GeneIdentifier(geneSetInfo.Id, geneName)
                select new GeneWithHash(new UniqueGeneIdentifier(GeneType.Fodder, geneId, architecture), geneHash))
            .Somes()
        let references = catletReference.Concat(fodderReferences)
        let contentMap = geneSetInfo.GeneDownloadInfo.ToSeq()
            .Map(dr => from hash in Gene.NewOption(dr.Gene)
                       from content in Optional(dr.Content).Filter(notEmpty)
                       select (hash, content))
            .Somes()
            .ToHashMap()
        from _ in catletReference.Concat(fodderReferences)
            .Map(g => CacheGene(g, contentMap, cancellationToken))
            .SequenceSerial()
        select result;

    private record GeneWithHash(UniqueGeneIdentifier UniqueGeneId, GeneHash Hash);

    private EitherAsync<Error, Unit> CacheGene(
        GeneWithHash geneWithHash,
        HashMap<Gene, string> contentMap,
        CancellationToken cancellationToken) =>
        from _ in contentMap.Find(geneWithHash.Hash.ToGene()).Match(
            Some: content => CacheGene(geneWithHash, content, cancellationToken),
            None: () => unit)
        select unit;

    private EitherAsync<Error, Unit> CacheGene(
        GeneWithHash geneWithHash,
        string content,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneWithHash.UniqueGeneId.Id.GeneSet)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneWithHash.UniqueGeneId)
        from _2 in TryAsync(async () =>
        {
            if (fileSystem.FileExists(genePath))
                return unit;

            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(genePath));
            await fileSystem.WriteAllTextAsync(genePath, content, cancellationToken);
            return unit;
        }).ToEither()
        from _3 in AddMergedGene(geneSetPath, geneWithHash.Hash)
        select unit;

    public EitherAsync<Error, GeneInfo> CacheGene(
        GeneInfo geneInfo,
        CancellationToken cancellationToken)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return from geneTempPath in GetGeneTempPath(genePoolPath, geneInfo.Id.Id.GeneSet, geneInfo.Hash)
               from result in TryAsync(async () =>
               {
                   fileSystem.EnsureDirectoryExists(geneTempPath);
               
                   await using var manifestStream = fileSystem.OpenWrite(Path.Combine(geneTempPath, "gene.json"));
                   await JsonSerializer.SerializeAsync(manifestStream, geneInfo.Manifest, cancellationToken: cancellationToken);
                   return geneInfo with
                   {
                       MergedWithImage = false
                   };
               
               }).ToEither()
               select result;
    }

    public EitherAsync<Error, Option<GeneSetInfo>> GetCachedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId)
        from manifest in TryAsync(async () =>
        {
            if (!fileSystem.FileExists(manifestPath))
                return None;

            await using var manifestStream = fileSystem.OpenRead(manifestPath);
            // TODO Dedicated serializer settings?
            var manifest = await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(
                manifestStream,
                cancellationToken: cancellationToken);

            return Some(manifest!);
        }).ToEither()
        select manifest.Map(m => new GeneSetInfo(geneSetId, m, []));

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
        GeneHash geneHash,
        GenePartHash genePartHash) =>
        from geneTempPath in GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, geneHash)
        let genePartPath = Path.Combine(geneTempPath, $"{genePartHash.Hash}.part")
        select genePartPath;

    public EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from geneSetManifest in TryAsync<Option<GenesetTagManifestData>>(async () =>
        {
            if (!fileSystem.FileExists(geneSetManifestPath))
                return None;

            var manifestJson = await fileSystem.ReadAllTextAsync(geneSetManifestPath, CancellationToken.None);
            var manifest = JsonSerializer.Deserialize<GenesetTagManifestData>(manifestJson);
            return manifest;
        }).ToEither()
        from genes in geneSetManifest
            .Map(GeneSetTagManifestUtils.GetGenes)
            .Sequence()
            .ToAsync()
        let geneHash = genes.Bind(g => g.Find(uniqueGeneId))
        from _2 in geneHash
            .Map(h => RemoveCachedGene(uniqueGeneId, h))
            .SequenceSerial()
        select unit;

    private EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from _2 in TryAsync(async () =>
        {
            var genes = await RemoveMergedGene(geneSetPath, geneHash.Value);
            if (genes.MergedGenes is not { Length: > 0 })
            {
                fileSystem.DeleteDirectory(geneSetPath);
            }
        }).ToEither()
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from _3 in Try<Unit>(() =>
        {
            var genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId);
            if (fileSystem.FileExists(genePath))
            {
                fileSystem.DeleteFile(genePath);
            }

            return unit;
        }).ToEitherAsync()
        select unit;

    private EitherAsync<Error, Seq<GeneHash>> GetMergedGenes(
        GeneSetIdentifier geneSetId) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId)
        from hashes in TryAsync(async () =>
        {
            var genesInfo = await ReadGenesInfo(geneSetPath);
            return genesInfo.MergedGenes.ToSeq();
        }).ToEither()
        from result in hashes
            .Map(GeneHash.NewEither)
            .Sequence()
            .ToAsync()
            .MapLeft(e => Error.New($"The The merged genes in the gene set '{geneSetId}' are invalid.", e))
        select result;

    public EitherAsync<Error, GeneHash> AddMergedGene(string geneSetPath, GeneHash geneHash) =>
        TryAsync(async () =>
        {
            var genesInfo = await ReadGenesInfo(geneSetPath);
            genesInfo.MergedGenes = [.. genesInfo.MergedGenes ?? [], geneHash.Value];
            await WriteGenesInfo(geneSetPath, genesInfo);
            return geneHash;
        }).ToEither();

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
        public string[]? MergedGenes { get; set; }
    }

    private static EitherAsync<Error, string> GetGeneTempPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId,
        GeneHash geneHash) =>
        from _ in RightAsync<Error, Unit>(unit)
        let tempPath = Path.Combine(
            GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId),
            geneHash.Hash)
        select tempPath;

    public EitherAsync<Error, Option<string>> GetCachedGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from geneExists in Try(() => fileSystem.FileExists(genePath))
            .ToEitherAsync()
        from content in geneExists
            ? TryAsync(() => fileSystem.ReadAllTextAsync(genePath,cancellationToken)).ToEither().Map(Optional)
            : RightAsync<Error, Option<string>>(None)
        select content;

    public EitherAsync<Error, string> CacheGeneContent(
        GeneContentInfo geneContentInfo,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        // TODO handle special case packed gene in temp folder -> this happens when copying genes directly in the local gene pool
        // Extract packed folder even before checking local
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneContentInfo.UniqueId.Id.GeneSet)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneContentInfo.UniqueId)
        from content in TryAsync(async () =>
        {
            // TODO should we always overwrite the existing file?
            if (fileSystem.FileExists(genePath))
                return await fileSystem.ReadAllTextAsync(genePath, cancellationToken);

            var geneFolder = Path.GetDirectoryName(genePath);
            fileSystem.EnsureDirectoryExists(geneFolder);
            await using var sourceStream = new MemoryStream(geneContentInfo.Content);
            await using var decompressionStream = CompressionStreamFactory.CreateDecompressionStream(
                sourceStream, geneContentInfo.Format);
            await using var targetStream = new MemoryStream();
            using var reader = new StreamReader(decompressionStream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync(cancellationToken);

            await fileSystem.WriteAllTextAsync(genePath, content, cancellationToken);

            return content;
        }).ToEither()
        from _2 in AddMergedGene(geneSetPath, geneContentInfo.Hash)
        select content;

    public EitherAsync<Error, GeneContentInfo> RetrieveGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Aff<CancelRt, Option<GeneSetInfo>> GetCachedGeneSet(
        GeneSetIdentifier geneSetId) =>
        from _ in SuccessAff(unit)
        let manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId)
        from manifest in Aff<CancelRt, Option<GenesetTagManifestData>>(async rt =>
        {
            if (!fileSystem.FileExists(manifestPath))
                return None;

            await using var manifestStream = fileSystem.OpenRead(manifestPath);
            // TODO Dedicated serializer settings?
            var manifest = await JsonSerializer.DeserializeAsync<GenesetTagManifestData>(
                manifestStream,
                GeneModelDefaults.SerializerOptions,
                cancellationToken: rt.CancellationToken);

            if (manifest is null)
                throw Error.New($"Could not deserialize the manifest of the gene set {geneSetId}.");

            return Some(manifest);
        })
        select manifest.Map(m => new GeneSetInfo(geneSetId, m, []));

    public Aff<CancelRt, Option<string>> GetCachedGeneContent(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in SuccessAff(unit)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from content in Aff<CancelRt, Option<string>>(async rt =>
        {
            if (!fileSystem.FileExists(genePath))
                return Option<string>.None;
            var content = await fileSystem.ReadAllTextAsync(genePath, rt.CancellationToken);

            return Some(content);
        })
        select content;

    public Aff<CancelRt, Option<GeneSetInfo>> GetGeneSet(
        GeneSetIdentifier geneSetId)
    {
        throw new NotImplementedException();
    }

    public Aff<CancelRt, Option<GeneContentInfo>> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash)
    {
        throw new NotImplementedException();
    }

    public Aff<CancelRt, Option<GenePartsInfo>> DownloadGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsInfo geneParts)
    {
        throw new NotImplementedException();
    }

    public Aff<CancelRt, string> CacheGeneContent(GeneContentInfo geneContentInfo)
    {
        throw new NotImplementedException();
    }

    public Aff<CancelRt, Option<GenePartsInfo>> GetDownloadedGeneParts(UniqueGeneIdentifier uniqueGeneId, GeneHash geneHash, Func<long, long, Task<Unit>> reportProgress)
    {
        throw new NotImplementedException();
    }
}
