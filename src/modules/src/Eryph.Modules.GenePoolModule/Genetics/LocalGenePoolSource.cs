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
using Eryph.GenePool.Compression;
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

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancel) =>
        from parsedGeneHash in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet)
        from genesInfo in TryAsync(() => ReadGenesInfo(geneSetPath)).ToEither()
        from geneInfo in genesInfo.MergedGenes.ToSeq().Find(h => h == geneHash.Value).Match(
            Some: _ => new GeneInfo(uniqueGeneId, geneHash, null, [], DateTimeOffset.MinValue, true, null),
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
                    if (hashString == geneHash.Hash.Value)
                        return manifestJsonData;
                    
                    fileSystem.FileDelete(cachedGeneManifestPath);
                    throw new VerificationException($"Failed to verify the hash of the manifest of '{uniqueGeneId}'");
                }).ToEither()
                from manifestData in Try(() => JsonSerializer.Deserialize<GeneManifestData>(manifestJson))
                    .ToEitherAsync()
                select new GeneInfo(uniqueGeneId, geneHash, manifestData, [], DateTimeOffset.MinValue, false, null))
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
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneInfo.Id.Id.GeneSet)
        from isGeneMerged in TryAsync(async () =>
        {
            var mergedGenesInfo = await ReadGenesInfo(geneSetPath);
            return mergedGenesInfo.MergedGenes.ToSeq().Contains(geneInfo.Hash.Value);
        }).ToEither()
        let parts = (geneInfo.Manifest?.Parts).ToSeq()
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
                      var decompression = new GeneDecompression(geneInfo, fileSystem, log, reportProgress);
                      await decompression.Decompress(multiStream, genePoolPath, cancellationToken);
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
                geneHash.Hash))
            .ToSeq()
        let fodderReferences = geneSetInfo.Manifest.FodderGenes.ToSeq()
            .Map(gene => from geneHash in GeneHash.NewOption(gene.Hash)
                         from geneName in GeneName.NewOption(gene.Name)
                from architecture in Architecture.NewOption(gene.Architecture)
                let geneId = new GeneIdentifier(geneSetInfo.Id, geneName)
                select new GeneWithHash(new UniqueGeneIdentifier(GeneType.Fodder, geneId, architecture), geneHash.Hash))
            .Somes()
        let references = catletReference.Concat(fodderReferences)
        let contentMap = geneSetInfo.GeneDownloadInfo.ToSeq()
            .Map(dr => from hash in Gene.NewOption(dr.Gene)
                       from content in Optional(dr.Content).Filter(notEmpty)
                       select (hash, content))
            .Somes()
            .ToHashMap()
        //from _ in catletReference.Concat(fodderReferences)
        //    .Map(g => CacheGene(g, contentMap, cancellationToken))
        //    .SequenceSerial()
        select result;

    private record GeneWithHash(UniqueGeneIdentifier UniqueGeneId, Gene Hash);

    private EitherAsync<Error, Unit> CacheGene(
        GeneWithHash geneWithHash,
        HashMap<Gene, string> contentMap,
        CancellationToken cancellationToken) =>
        from _ in contentMap.Find(geneWithHash.Hash).Match(
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
        // TODO Fix the nasty Gene -> GeneHash conversion
        from _3 in AddMergedGene(geneSetPath, GeneHash.New($"sha256:{geneWithHash.Hash}"))
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
        let genePartPath = Path.Combine(geneTempPath, $"{genePartHash.Hash.Value}.part")
        select genePartPath;

    public EitherAsync<Error, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId) =>
        from _ in TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, uniqueGeneId.Id.GeneSet);
            var manifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, uniqueGeneId.Id.GeneSet);
            if (!fileSystem.FileExists(manifestPath))
                return unit;

            var manifestJson = await fileSystem.ReadAllTextAsync(manifestPath, CancellationToken.None);
            var manifest = JsonSerializer.Deserialize<GenesetTagManifestData>(manifestJson);

            var geneHash = GeneSetTagManifestUtils.FindGeneHash(
                manifest, uniqueGeneId.GeneType, uniqueGeneId.Id.GeneName, uniqueGeneId.Architecture);
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
            geneHash.Hash.Value)
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
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneContentInfo.UniqueId.Id.GeneSet)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneContentInfo.UniqueId)
        from content in TryAsync(async () =>
        {
            // TODO should we always overwrite the existing file?
            if (fileSystem.FileExists(genePath))
                return await fileSystem.ReadAllTextAsync(genePath, cancellationToken);


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

    public EitherAsync<Error, GeneContentInfo> RetrieveGeneContent(UniqueGeneIdentifier uniqueGeneId, GeneHash geneHash, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
