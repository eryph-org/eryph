using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class RepositoryGenePool : GenePoolBase, IGenePool
{

    public string? PoolName { get; set; }
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _log;
    private readonly IFileSystemService _fileSystem;

    public RepositoryGenePool(IHttpClientFactory httpClientFactory, ILogger log, IFileSystemService fileSystem)
    {
        _httpClientFactory = httpClientFactory;
        _log = log;
        _fileSystem = fileSystem;
    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier genesetIdentifier, CancellationToken cancel)
    {

        return Prelude.TryAsync(async () =>
            {
                var manifestUrl =
                    $"{genesetIdentifier.Organization}/{genesetIdentifier.GeneSet}/{genesetIdentifier.Tag}/geneset.json";
                using var httpClient = _httpClientFactory.CreateClient(PoolName);

                var response = await httpClient.GetAsync(manifestUrl, cancel);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return Error.New($"Could not find geneset '{genesetIdentifier}' on {PoolName}.");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return Error.New(
                        $"Failed to connect to {PoolName}. Received a {response.StatusCode} HTTP response.");
                }

                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
                if (contentLength == 0)
                    return Error.New($"Could not find geneset '{genesetIdentifier}' on {PoolName}.");

                var manifest = ReadGeneSetManifest(await response.Content.ReadAsStringAsync(cancel));

                if (manifest?.GeneSet != genesetIdentifier.Name)
                {
                    return Error.New($"Invalid manifest for geneset '{genesetIdentifier}'.");
                }

                return await Prelude.RightAsync<Error, GeneSetInfo>(new GeneSetInfo(genesetIdentifier, "",
                    manifest));
            }).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide geneset {geneset} from gene pool {genepool}", genesetIdentifier,
                    PoolName);
                return Error.New(ex);
            })
            .Bind(e => e.ToAsync());
    }

    public EitherAsync<Error, GeneInfo> RetrieveGene(GeneSetInfo genesetInfo, GeneIdentifier geneIdentifier, string geneHash, CancellationToken cancel)
    {
        return Prelude.TryAsync(() =>
                ParseGeneHash(geneHash).BindAsync(async parsedGeneId =>
                    {
                        var (hashAlgName, partHash) = parsedGeneId;

                        var geneUrl =
                            $"{genesetInfo.Id.Organization}/{genesetInfo.Id.GeneSet}/{genesetInfo.Id.Tag}/{partHash}/gene.json";
                        _log.LogTrace("gene {gene} manifest url: {url}", geneIdentifier, geneUrl);

                        using var httpClient = _httpClientFactory.CreateClient(PoolName);
                        var response = await httpClient.GetAsync(geneUrl, cancel);

                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return Error.New($"Could not find {geneIdentifier} on {PoolName}.");

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            return Error.New(
                                $"Failed to connect to {PoolName}. Received a {response.StatusCode} HTTP response.");
                        }

                        var manifestContent = await response.Content.ReadAsStringAsync(cancel);
                        var hash = CreateHashAlgorithm(hashAlgName);
                        var hashString = GetHashString(hash.ComputeHash(Encoding.UTF8.GetBytes(manifestContent)));

                        if (hashString != partHash)
                            return Error.New($"Failed to validate integrity of {geneIdentifier}.");

                        var manifest = ReadGeneManifest(manifestContent);

                        return await Prelude.RightAsync<Error, GeneInfo>(
                            new GeneInfo(geneIdentifier,
                                partHash, hashAlgName, manifest, null, false));
                    }
                )).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide gene '{gene}' from gene pool {source}", geneIdentifier, PoolName);
                return Error.New(ex);
            })
            .Bind(e => e.ToAsync());
    }

    public EitherAsync<Error, long> RetrieveGenePart(GeneInfo geneInfo, string genePart,
        long availableSize, long totalSize, Func<string, Task<Unit>> reportProgress, Stopwatch stopwatch, CancellationToken cancel)
    {

        return ParseGenePartHash(genePart).BindAsync(async parsedPartName =>
        {
            var (hashAlgName, partHash) = parsedPartName;

            var messageName = $"{geneInfo}/{partHash[..12]}";
            var genesetId = geneInfo.GeneId.GeneSet;
            var genePartUrl =
                $"{genesetId.Organization}/{genesetId.GeneSet}/{genesetId.Tag}/{geneInfo.Hash}/{partHash}.part";
            _log.LogTrace("gene {gene}, part {genePart} url: {url}", geneInfo,
                genePart, genePartUrl);

            using var httpClient = _httpClientFactory.CreateClient(PoolName);
            var response = await httpClient.GetAsync(genePartUrl, HttpCompletionOption.ResponseHeadersRead, cancel);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Error.New($"Could not find gene part {messageName} on {PoolName}.");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Error.New($"Failed to connect to gene pool. Received a {response.StatusCode} HTTP response.");
            }

            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
            _log.LogTrace("gene part {gene}/{part} content length: {contentLength}", geneInfo, partHash, contentLength);

            if (contentLength == 0)
                return Error.New($"Could not find gene part '{messageName}' on {PoolName}.");

            var partFile = Path.Combine(geneInfo.LocalPath, $"{partHash}.part");

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancel);
            var hashAlg = CreateHashAlgorithm(hashAlgName);
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var tempFileStream = _fileSystem.OpenWrite(partFile))
            {

                var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
                await CopyToAsync(cryptoStream, tempFileStream, reportProgress,
                    geneInfo.GeneId,
                    availableSize, totalSize, stopwatch, cancel: cancel);

            }

            var hashString = GetHashString(hashAlg.Hash);
            _log.LogTrace("gene part {part} hash: {hashString}", messageName, hashString);

            if (hashString != partHash)
            {
                _log.LogInformation("gene part '{part}' hash mismatch. Actual hash: {hashString}",
                    messageName,
                    hashString);

                _fileSystem.FileDelete(partFile);
                return Error.New($"Failed to verify hash of gene part '{messageName}'");
            }

            return await Prelude.RightAsync<Error, long>(_fileSystem.GetFileSize(partFile)).ToEither();


        }).ToAsync();

    }



    private async Task CopyToAsync(Stream source, Stream destination, Func<string, Task<Unit>> reportProgress, GeneIdentifier geneIdentifier, long availableSize, long totalSize, 
        Stopwatch stopwatch, int bufferSize = 65536, CancellationToken cancel = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        int bytesRead;
        long totalRead = 0;
        var totalMb = totalSize / 1024d / 1024d;
        
        while ((bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancel)) > 0)
        {
            cancel.ThrowIfCancellationRequested();

            await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer,0, bytesRead), cancel);
            totalRead += bytesRead;

            if(stopwatch.Elapsed.TotalSeconds <= 10 || totalMb == 0)
                continue;

            var totalReadMb = Math.Round((availableSize+ totalRead) / 1024d / 1024d, 0);
            var percent = totalReadMb / totalMb;

            _log.LogTrace("Pulling {geneIdentifier} ({totalReadMb} MB / {totalMb} MB) => {percent} completed", geneIdentifier, totalReadMb, totalMb, percent);
            await reportProgress($"Pulling {geneIdentifier} ({totalReadMb:F} MB / {totalMb:F} MB) => {percent:P0} completed");
            stopwatch.Restart();

        }
    }

}