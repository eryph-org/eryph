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
                    using var hashAlg = CreateHashAlgorithm(parsedGeneHash.HashAlg);

                    var manifestJsonData = await fileSystem.ReadAllTextAsync(cachedGeneManifestPath);
                    var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));
                    if (hashString == parsedGeneHash.Hash)
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
        Func<string, int, Task<Unit>> reportProgress,
        CancellationToken cancel) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneInfo.Id.Id.GeneSet)
        from isGeneMerged in TryAsync(async () =>
        {
            var mergedGenesInfo = await ReadGenesInfo(geneSetPath);
            return mergedGenesInfo.MergedGenes.ToSeq().Contains(geneInfo.Hash);
        }).ToEither()
        let parts = (geneInfo.MetaData?.Parts).ToSeq()
        from _2 in parts.IsEmpty || isGeneMerged
            ? RightAsync<Error, Unit>(unit)
            : from partPaths in parts
                .Map(part => GetGenePartPath(geneInfo.Id, geneInfo.Hash, part))
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
                      await decompression.Decompress(multiStream, genePoolPath, cancel);
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
              from _2 in TryAsync(async () =>
              {
                  await AddMergedGene(geneSetPath, geneInfo.Hash);
                  return unit;
              }).ToEither()
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
        CancellationToken cancel)
    {
        return TryAsync(async () =>
        {
            var geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetInfo.Id);
            var geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetInfo.Id);
            fileSystem.EnsureDirectoryExists(geneSetPath);
            
            await using var manifestStream = fileSystem.OpenWrite(geneSetManifestPath);
            await JsonSerializer.SerializeAsync(manifestStream, geneSetInfo.MetaData, cancellationToken: cancel);
            return geneSetInfo;

        }).ToEither();
    }

    public EitherAsync<Error, GeneInfo> CacheGene(
        GeneInfo geneInfo,
        CancellationToken cancel)
    {
        if (geneInfo.MergedWithImage)
            return geneInfo;

        return from geneTempPath in GetGeneTempPath(genePoolPath, geneInfo.Id.Id.GeneSet, geneInfo.Hash)
               from result in TryAsync(async () =>
               {
                   fileSystem.EnsureDirectoryExists(geneTempPath);
               
                   await using var manifestStream = fileSystem.OpenWrite(Path.Combine(geneTempPath, "gene.json"));
                   await JsonSerializer.SerializeAsync(manifestStream, geneInfo.MetaData, cancellationToken: cancel);
                   return geneInfo with
                   {
                       MergedWithImage = false
                   };
               
               }).ToEither()
               select result;
    }

    public EitherAsync<Error, GeneSetInfo> GetCachedGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
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
        select new GeneSetInfo(geneSetId, manifest, []);

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
        from parsedPartHash in ParseGenePartHash(genePartHash).ToAsync()
        from geneTempPath in GetGeneTempPath(genePoolPath, uniqueGeneId.Id.GeneSet, geneHash)
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

    private async Task<string> AddMergedGene(string geneSetPath, string geneHash)
    {
        var genesInfo = await ReadGenesInfo(geneSetPath);
        genesInfo.MergedGenes = [..genesInfo.MergedGenes ?? [], geneHash];
        await WriteGenesInfo(geneSetPath, genesInfo);
        return geneHash;
    }

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

    private static EitherAsync<Error, string> GetGeneTempPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId,
        string geneHash) =>
        from parsedGeneHash in ParseGeneHash(geneHash).ToAsync()
        let path = Path.Combine(
            GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId),
            parsedGeneHash.Hash)
        select path;
}
