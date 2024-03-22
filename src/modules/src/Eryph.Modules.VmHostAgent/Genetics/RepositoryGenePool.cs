using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.GenePool.Client;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;
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

    private static EitherAsync<Error, GenePoolClient> CreateClient()
    {
        return Prelude.TryAsync(async () =>
        {
            var genePoolUri = new Uri("https://eryphgenepoolapistaging.azurewebsites.net/api/");

            // todo: add support for authentication
            var client = new GenePoolClient(genePoolUri);
            return client;
        }).ToEither();
    }

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(string path, GeneSetIdentifier genesetIdentifier,
        CancellationToken cancel)
    {
        return from genePoolClient in CreateClient()
            from geneSetInfo in Prelude.TryAsync(async () =>
            {
                var genesetTagClient = genePoolClient.GetGenesetTagClient(genesetIdentifier);
                var response = await genesetTagClient.GetForDownloadAsync(cancel)
                               ?? throw new InvalidDataException("empty response from geneset api");

                return new GeneSetInfo(genesetIdentifier, "", response.Manifest, response.Genes);
            }).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide geneset {geneset} from gene pool {genepool}", genesetIdentifier,
                    PoolName);
                return Error.New(ex);
            })
            select geneSetInfo;


    }

    public EitherAsync<Error, GeneInfo> RetrieveGene(GeneSetInfo genesetInfo, GeneIdentifier geneIdentifier,
        string geneHash, CancellationToken cancel)
    {
        return from parsedGeneId in ParseGeneHash(geneHash).ToAsync()
            from genePoolClient in CreateClient()
            from geneInfo in Prelude.TryAsync(async () =>
            {
                var downloadEntry = genesetInfo.GeneDownloadInfo.FirstOrDefault(x =>
                    x.Gene == parsedGeneId.Hash);

                if (downloadEntry == null)
                {
                    var geneClient = genePoolClient.GetGeneClient(geneIdentifier.GeneSet.Value, parsedGeneId.Hash);

                    var response = await geneClient.GetAsync(cancel)
                                   ?? throw new InvalidDataException("empty response from gene api");

                    downloadEntry = new GetGeneDownloadResponse(parsedGeneId.Hash, response.Manifest,
                        response.DownloadUris, response.DownloadExpires.GetValueOrDefault());
                }

                return new GeneInfo(geneIdentifier, parsedGeneId.Hash,
                    parsedGeneId.HashAlg, downloadEntry.Manifest,
                    downloadEntry.DownloadUris, downloadEntry.DownloadExpires,
                    null, false);

            }).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide gene '{gene}' from gene pool {source}", geneIdentifier, PoolName);
                return Error.New(ex);
            })
            select geneInfo;

    }

    public EitherAsync<Error, long> RetrieveGenePart(GeneInfo geneInfo, string genePart,
        long availableSize, long totalSize, Func<string, int, Task<Unit>> reportProgress, Stopwatch stopwatch,
        CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(geneInfo.LocalPath))
            throw new ArgumentException("local path of gene is not set.", nameof(geneInfo));

        return from parsedPartId in ParseGenePartHash(genePart).ToAsync()
            from genePoolClient in CreateClient()
            from genePartUrl in Prelude.TryAsync(async () =>
            {
                var urlEntry = (geneInfo.DownloadUris ?? Array.Empty<GenePartDownloadUri>())
                    .FirstOrDefault(x => x.Part == genePart);

                if (urlEntry != null)
                {
                    var expiresLocal = DateTimeOffset.UtcNow.AddMinutes(5);
                    if (geneInfo.DownloadExpires > expiresLocal)
                        return urlEntry.DownloadUri;
                }

                var gene = genePoolClient.GetGeneClient(geneInfo.GeneId.GeneSet.Value, geneInfo.Hash);
                var response = await gene.GetAsync(cancel)
                               ?? throw new InvalidDataException("empty response from gene api");
                urlEntry = (response.DownloadUris ?? Array.Empty<GenePartDownloadUri>())
                    .FirstOrDefault(x => x.Part == genePart);

                if (urlEntry == null)
                    throw new InvalidDataException(
                        $"Could not find gene part '{geneInfo.GeneId}/{parsedPartId.Hash}' on {PoolName}.");

                return urlEntry.DownloadUri;

            }).ToEither()
            from fileSize in Prelude.TryAsync(async () =>
            {
                var (hashAlgName, partHash) = parsedPartId;
                var messageName = $"{geneInfo}/{partHash[..12]}";

                _log.LogTrace("gene {gene}, part {genePart} url: {url}", geneInfo,
                    genePart, genePartUrl);

                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(genePartUrl, HttpCompletionOption.ResponseHeadersRead, cancel);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidDataException($"Could not find gene part {messageName} on {PoolName}.");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidDataException(
                        $"Failed to connect to gene pool. Received a {response.StatusCode} HTTP response.");
                }

                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
                _log.LogTrace("gene part {gene}/{part} content length: {contentLength}", geneInfo, partHash,
                    contentLength);

                if (contentLength == 0)
                    throw new InvalidDataException($"Could not find gene part '{messageName}' on {PoolName}.");

                var partFile = Path.Combine(geneInfo.LocalPath, $"{partHash}.part");

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancel);
                using var hashAlg = CreateHashAlgorithm(hashAlgName);
                // ReSharper disable once ConvertToUsingDeclaration
                await using (var tempFileStream = _fileSystem.OpenWrite(partFile))
                {
                    await using var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
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
                    throw new HashVerificationException($"Failed to verify hash of gene part '{messageName}'");
                }

                return _fileSystem.GetFileSize(partFile);

            }).ToEither()
            select fileSize;

    }



    private async Task CopyToAsync(Stream source, Stream destination, Func<string, int, Task<Unit>> reportProgress,
        GeneIdentifier geneIdentifier, long availableSize, long totalSize,
        Stopwatch stopwatch, int bufferSize = 65536, CancellationToken cancel = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {

            int bytesRead;
            long totalRead = 0;
            var totalMb = totalSize / 1024d / 1024d;

            while ((bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancel)) > 0)
            {
                cancel.ThrowIfCancellationRequested();

                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancel);
                totalRead += bytesRead;

                if (stopwatch.Elapsed.TotalSeconds <= 10 || totalMb == 0)
                    continue;

                var totalReadMb = Math.Round((availableSize + totalRead) / 1024d / 1024d, 0);
                var percent = totalReadMb / totalMb;
                var percentInt = Convert.ToInt32(Math.Round(percent * 100, 0));

                _log.LogTrace("Pulling {geneIdentifier} ({totalReadMb} MB / {totalMb} MB) => {percent} completed",
                    geneIdentifier, totalReadMb, totalMb, percent);
                await reportProgress(
                    $"Pulling {geneIdentifier} ({totalReadMb:F} MB / {totalMb:F} MB) => {percent:P0} completed",
                    percentInt);
                stopwatch.Restart();

            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

    }
}