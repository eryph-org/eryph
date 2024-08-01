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
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Model.Responses;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class RepositoryGenePool(
    IHttpClientFactory httpClientFactory,
    ILogger log,
    IFileSystemService fileSystem,
    IGenePoolApiKeyStore keyStore,
    IApplicationInfoProvider applicationInfo,
    IHardwareIdProvider hardwareIdProvider,
    string poolName)
    : GenePoolBase, IGenePool
{
    private const int BufferSize = 65536;

    public string PoolName => poolName;

    private EitherAsync<Error, GenePoolClient> CreateClient() =>
        from apiKey in keyStore.GetApiKey(PoolName)
        let clientOptions = new GenePoolClientOptions()
        {
            Diagnostics =
            {
                ApplicationId = applicationInfo.ApplicationId,
            },
            HardwareId = hardwareIdProvider.HashedHardwareId,
        }
        from client in Try(() => apiKey.Match(
                Some: key => new GenePoolClient(
                    GenePoolConstants.EryphGenePool.ApiEndpoint,
                    new ApiKeyCredential(key.Secret),
                    clientOptions),
                None: () => new GenePoolClient(
                    GenePoolConstants.EryphGenePool.ApiEndpoint,
                    clientOptions)))
            .ToEither(ex => Error.New("Could not create the gene pool API client.", ex)).ToAsync()
        select client;

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        string path,
        GeneSetIdentifier geneSetIdentifier,
        CancellationToken cancel) =>
        from genePoolClient in CreateClient()
        from geneSetInfo in TryAsync(async () =>
        {
            var genesetTagClient = genePoolClient.GetGenesetTagClient(geneSetIdentifier);
            var response = await genesetTagClient.GetForDownloadAsync(cancel)
                           ?? throw new InvalidDataException("empty response from geneset api");

            return new GeneSetInfo(geneSetIdentifier, "", response.Manifest, response.Genes);
        }).ToEither(ex =>
        {
            log.LogDebug(ex, "Failed to provide geneset {geneset} from gene pool {genepool}", geneSetIdentifier,
                PoolName);
            return Error.New(ex);
        })
        select geneSetInfo;

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        GeneSetInfo geneSetInfo,
        GeneIdentifier geneIdentifier,
        string geneHash,
        CancellationToken cancel) =>
        from parsedGeneId in ParseGeneHash(geneHash).ToAsync()
        from genePoolClient in CreateClient()
        from geneInfo in TryAsync(async () =>
        {
            var downloadEntry = geneSetInfo.GeneDownloadInfo.FirstOrDefault(x =>
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
            log.LogDebug(ex, "Failed to provide gene '{gene}' from gene pool {source}", geneIdentifier, PoolName);
            return Error.New(ex);
        })
        select geneInfo;

    public EitherAsync<Error, long> RetrieveGenePart(
        GeneInfo geneInfo,
        string genePartHash,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel) =>
        from parsedPartId in ParseGenePartHash(genePartHash).ToAsync()
        from _ in guard(notEmpty(geneInfo.LocalPath),
            Error.New("The local path was not provided with the gene.")).ToEitherAsync()
        from genePoolClient in CreateClient()
        from genePartUrl in TryAsync(async () =>
        {
            var urlEntry = geneInfo.DownloadUris?.FirstOrDefault(x => x.Part == genePartHash);

            if (urlEntry != null)
            {
                var expiresLocal = DateTimeOffset.UtcNow.AddMinutes(5);
                if (geneInfo.DownloadExpires > expiresLocal)
                    return urlEntry.DownloadUri;
            }

            var gene = genePoolClient.GetGeneClient(geneInfo.GeneId.GeneSet.Value, geneInfo.Hash);
            var response = await gene.GetAsync(cancel)
                           ?? throw new InvalidDataException("empty response from gene api");
            urlEntry = response.DownloadUris?.FirstOrDefault(x => x.Part == genePartHash);

            if (urlEntry == null)
                throw new InvalidDataException(
                    $"Could not find gene part '{geneInfo.GeneId}/{parsedPartId.Hash}' on {PoolName}.");

            return urlEntry.DownloadUri;

        }).ToEither()
        from fileSize in TryAsync(async () =>
        {
            var (hashAlgName, partHash) = parsedPartId;
            var messageName = $"{geneInfo}/{partHash[..12]}";

            log.LogTrace("gene {Gene}, part {GenePart} url: {Url}",
                geneInfo, genePartHash, genePartUrl);

            using var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(genePartUrl, HttpCompletionOption.ResponseHeadersRead, cancel);

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidDataException($"Could not find gene part {messageName} on {PoolName}.");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidDataException(
                    $"Failed to connect to gene pool. Received a {response.StatusCode} HTTP response.");
            }

            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
            log.LogTrace("gene part {Gene}/{Part} content length: {ContentLength}",
                geneInfo, partHash, contentLength);

            if (contentLength == 0)
                throw new InvalidDataException($"Could not find gene part '{messageName}' on {PoolName}.");

            var partFile = Path.Combine(geneInfo.LocalPath, $"{partHash}.part");

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancel);
            using var hashAlg = CreateHashAlgorithm(hashAlgName);
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var tempFileStream = fileSystem.OpenWrite(partFile))
            {
                await using var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
                await CopyToAsync(cryptoStream, tempFileStream, reportProgress,
                    geneInfo.GeneId,
                    availableSize, totalSize, stopwatch, cancel: cancel);
            }

            var hashString = GetHashString(hashAlg.Hash);
            log.LogTrace("gene part {Part} hash: {HashString}", messageName, hashString);

            if (hashString == partHash)
                return fileSystem.GetFileSize(partFile);
            
            log.LogInformation("gene part '{Part}' hash mismatch. Actual hash: {HashString}",
                messageName, hashString);

            fileSystem.FileDelete(partFile);
            throw new HashVerificationException($"Failed to verify hash of gene part '{messageName}'");
        }).ToEither()
        select fileSize;

    private async Task CopyToAsync(Stream source, Stream destination, Func<string, int, Task<Unit>> reportProgress,
        GeneIdentifier geneIdentifier, long availableSize, long totalSize,
        Stopwatch stopwatch, CancellationToken cancel = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long totalRead = 0;
            var totalMb = totalSize / 1024d / 1024d;

            while (true)
            {
                // We are (indirectly) reading from a stream provided by the HTTP client.
                // In this case, the read operation might block indefinitely if the network
                // connection is lost. Hence, we explicitly define a reasonable timeout.
                // See https://github.com/dotnet/runtime/issues/36822.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutCts.Token);
                var bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cts.Token);
                if (bytesRead <= 0)
                    return;

                cancel.ThrowIfCancellationRequested();

                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancel);
                totalRead += bytesRead;

                if (stopwatch.Elapsed.TotalSeconds <= 10 || totalMb == 0)
                    continue;

                var totalReadMb = Math.Round((availableSize + totalRead) / 1024d / 1024d, 0);
                var percent = totalReadMb / totalMb;
                var percentInt = Convert.ToInt32(Math.Round(percent * 100, 0));

                log.LogTrace("Pulling {GeneIdentifier} ({TotalReadMb} MB / {TotalMb} MB) => {Percent} completed",
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
