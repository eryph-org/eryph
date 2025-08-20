using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenePool.Genetics;

internal class RepositoryGenePool(
    IHttpClientFactory httpClientFactory,
    ILogger log,
    IFileSystemService fileSystem,
    IGenePoolApiKeyStore keyStore,
    IApplicationInfoProvider applicationInfo,
    IHardwareIdProvider hardwareIdProvider,
    GenePoolSettings genepoolSettings)
    : IGenePool
{
    // TODO Define proper error code
    private static readonly Error UrlExpiredError = (unchecked((int)0x8000_0000), "The gene pool download Url expires soon.");

    private const int DirectDownloadMaxSize = 5 * 1024 * 1024;
    private const int BufferSize = 1 * 1024 * 1024;

    public string PoolName => genepoolSettings.Name;

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
                    genepoolSettings.ApiEndpoint,
                    new ApiKeyCredential(key.Secret),
                    clientOptions),
                None: () => new GenePoolClient(
                    genepoolSettings.ApiEndpoint,
                    clientOptions)))
            .ToEither(ex => Error.New("Could not create the gene pool API client.", ex)).ToAsync()
        select client;

    public Aff<CancelRt, Option<Unit>> DownloadGene2(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task<Unit>> reportProgress) =>
        retryWhile(
            // This retry is triggered when the download URL is about to expire.
            // We just fetch new download URLs and retry immediately. We only limit
            // the number of retries in case the gene pool behaves incorrectly and
            // e.g. returns already expired URLs.
            Schedule.Forever & Schedule.recurs(5),
            from repositoryGeneInfo in FetchGene(uniqueGeneId, geneHash)
            from result in repositoryGeneInfo
                .Map(i => DownloadGene2(uniqueGeneId, geneHash, partsState, downloadPath, reportProgress, i))
                .Sequence()
            select result,
            e => e.Is(UrlExpiredError));

    private Aff<CancelRt, Unit> DownloadGene2(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task<Unit>> reportProgress,
        RepositoryGeneInfo geneRepositoryInfo) =>
        from _1 in SuccessAff(unit)
        let manifestPath = GenePoolPaths.GetTempGeneManifestPath(downloadPath)
        from _ in Aff<CancelRt, Unit>(async rt =>
        {
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(manifestPath)!);
            var json = JsonSerializer.Serialize(geneRepositoryInfo.Manifest, GeneModelDefaults.SerializerOptions);
            await fileSystem.WriteAllTextAsync(manifestPath, json, rt.CancellationToken);
            return unit;
        })
        from allParts in GeneManifestUtils.GetParts(geneRepositoryInfo.Manifest)
            .ToAff(e => Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash}) contains invalid parts.", e))
        from existingParts in partsState.GetExistingParts()
        let missingParts = allParts.Except(existingParts.Keys)
        from result in missingParts.ToSeq()
            .Map(part => DownloadGenePart2(uniqueGeneId, geneHash, part, geneRepositoryInfo, partsState, downloadPath, reportProgress))
            .SequenceSerial()
        select unit;

    private Aff<CancelRt, Unit> DownloadGenePart2(
        UniqueGeneIdentifier geneId,
        GeneHash geneHash,
        GenePartHash genePartHash,
        RepositoryGeneInfo repositoryGeneInfo,
        GenePartsState partsState,
        string downloadPath,
        Func<long, long, Task<Unit>> reportProgress) =>
        from url in repositoryGeneInfo.DownloadUris
            .Find(genePartHash)
            .ToAff(Error.New($"The gene part {genePartHash.Hash} is not available in the gene pool."))
        // URLs which expire in less than 5 minutes are considered unusable to avoid failures in
        // case of clock differences.
        from _  in guard(
            repositoryGeneInfo.DownloadExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5),
            UrlExpiredError)
        let path = GenePoolPaths.GetTempGenePartPath(downloadPath, genePartHash)
        from totalBytes in Optional(repositoryGeneInfo.Manifest.Size)
            .ToAff(Error.New($"The manifest of the gene {geneId} ({geneHash}) does not contain the size."))
        from downloadedParts in partsState.GetExistingParts()
        let downloadedBytes = downloadedParts.Values.ToSeq().Sum()
        from size in Aff<CancelRt, long>(async rt =>
            {
                try
                {
                    log.LogTrace("Downloading gene part {GenePart} from {Url}", genePartHash, url);
                    await using var fileStream = fileSystem.OpenWrite(path);
                    // Even with a large buffer (1 MiB), read operations from the HTTP response stream
                    // only return small chunks (16 KiB). We use a BufferedStream to make sure
                    // that we write to the file system in larger chunks.
                    await using var bufferStream = new BufferedStream(fileStream, BufferSize);
                    await using var progressStream = new ProgressStream(
                        bufferStream,
                        TimeSpan.FromSeconds(10),
                        async (progress, _) =>
                        {
                            await reportProgress(downloadedBytes + progress, totalBytes);
                        });
                    await FetchGenePart(progressStream, url, genePartHash, rt.CancellationToken);
                    return fileSystem.GetFileSize(path);
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex, "Failed to download gene part {GenePart} from {Url}", genePartHash, url);
                    fileSystem.FileDelete(path);
                    throw;
                }
            })
        from _2 in partsState.AddPart(genePartHash, size)
        select unit;

    private async Task FetchGenePart(
        Stream targetStream,
        Uri genePartUri,
        GenePartHash genePartHash,
        CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient(GenePoolConstants.PartClientName);
        var response = await httpClient.GetAsync(genePartUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw Error.New($"Could not find gene part {genePartHash.Hash} on gene pool {PoolName}.");

        if (response.StatusCode != HttpStatusCode.OK)
            throw Error.New($"Failed to connect to gene pool {PoolName}. Received a {response.StatusCode} HTTP response.");

        if (response.Content.Headers.ContentLength is not > 0)
            throw Error.New($"The gene pool {PoolName} did not provide any content for the gen part {genePartHash.Hash}.");

        var hashAlgorithm = genePartHash.CreateAlgorithm();
        await using var cryptoStream = new CryptoStream(targetStream, hashAlgorithm, CryptoStreamMode.Write);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await CopyToAsync(responseStream, cryptoStream, cancellationToken);

        await cryptoStream.FlushFinalBlockAsync(cancellationToken);
        var actualGenePartHash = hashAlgorithm.ToGenePartHash();

        if (genePartHash != actualGenePartHash)
            throw new HashVerificationException("Failed to verify the hash of the gene part. Maybe it got corrupted during transfer.");
    }

    private async Task CopyToAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(BufferSize);
        var buffer = memoryOwner.Memory;
        
        while (true)
        {
            // We are (indirectly) reading from a stream provided by the HTTP client.
            // In this case, the read operation might block indefinitely if the network
            // connection is lost. Hence, we explicitly define a reasonable timeout.
            // See https://github.com/dotnet/runtime/issues/36822.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var bytesRead = await source.ReadAsync(buffer, combinedCts.Token);
            combinedCts.Token.ThrowIfCancellationRequested();
            
            if (bytesRead <= 0)
                return;

            await destination.WriteAsync(buffer[..bytesRead], cancellationToken);
        }
    }

    private Aff<CancelRt, Option<RepositoryGeneInfo>> FetchGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from genePoolClient in CreateClient().ToAff()
        from response in Aff<CancelRt, GetGeneResponse>(async rt =>
            {
                try
                {
                    var geneClient = genePoolClient.GetGeneClient(uniqueGeneId.Id.GeneSet, geneHash.ToGene());
                    var response = await geneClient.GetAsync(cancellationToken: rt.CancellationToken);
                    if (response is null)
                        throw Error.New("The response from the gene pool API is empty.");

                    log.LogDebug("Found gene {GeneId} ({GeneHash}) on gene pool '{GenePool}'",
                        uniqueGeneId, geneHash, PoolName);
                    return response;
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex, "Failed to lookup gene {GeneId} ({GeneHash}) on gene pool '{GenePool}'",
                        uniqueGeneId, geneHash, PoolName);
                    throw;
                }
            })
            .Map(Some)
            .Catch(e => IsNotFoundError(e), SuccessAff<Option<GetGeneResponse>>(None))
        from result in response
            .Map(r => CreateRepositoryGeneInfo(uniqueGeneId, geneHash, r))
            .Sequence()
        select result;

    private Eff<RepositoryGeneInfo> CreateRepositoryGeneInfo(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GetGeneResponse response) =>
        from manifest in Optional(response.Manifest)
            .ToEff(Error.New("The gene manifest is missing in the response of the gene pool API."))
        let manifestHash = GeneManifestUtils.ComputeHash(manifest)
        from _ in guard(manifestHash == geneHash,
            Error.New($"The gene manifest hash {manifestHash} does not match the requested gene hash {geneHash}."))
        from downloadExpiresAt in Optional(response.DownloadExpires)
            .ToEff(Error.New("The download expiration time is missing in the response of the gene pool API."))
        from downloadUris in response.DownloadUris.ToSeq()
            .Map(u => from partHash in GenePartHash.NewValidation(u.Part)
                      select (partHash, u.DownloadUri))
            .Sequence()
            .ToEff(errors => Error.New("Some of the download URIs returned by the gene pool API are invalid.", Error.Many(errors)))
        select new RepositoryGeneInfo(manifest, downloadUris.ToHashMap(), downloadExpiresAt);

    private record RepositoryGeneInfo(
        GeneManifestData Manifest,
        HashMap<GenePartHash, Uri> DownloadUris,
        DateTimeOffset DownloadExpiresAt);

    public Aff<CancelRt, Option<GeneSetInfo>> GetGeneSet(
        GeneSetIdentifier geneSetId) =>
        from genePoolClient in CreateClient().ToAff()
        from response in AffMaybe<CancelRt, GenesetTagDownloadResponse>(
            async rt =>
            {
                try
                {
                    var genesetTagClient = genePoolClient.GetGenesetTagClient(geneSetId);
                    var response = await genesetTagClient.GetForDownloadAsync(cancellationToken: rt.CancellationToken);
                    if (response is null)
                        return Error.New("The response from the gene pool API is empty.");

                    log.LogDebug("Found geneset {GeneSetId} on gene pool '{GenePoolName}'", geneSetId, PoolName);
                    return FinSucc(response);
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex, "Failed to lookup geneset {GeneSetId} on gene pool '{GenePool}'", geneSetId,
                        PoolName);
                    throw;
                }
            })
            .Map(Some)
            .Catch(e => IsNotFoundError(e), SuccessAff<Option<GenesetTagDownloadResponse>>(None))
        from result in response.Map(r => CreateGeneSetInfo(geneSetId, r))
            .Sequence()
        select result;

    private Eff<GeneSetInfo> CreateGeneSetInfo(
        GeneSetIdentifier geneSetId,
        GenesetTagDownloadResponse response) =>
        from manifest in Optional(response.Manifest)
            .ToEff(Error.New("The gene set manifest is missing in the response of the gene pool API."))
        select new GeneSetInfo(
            geneSetId,
            manifest,
            response.Genes.ToSeq().Strict());


    public Aff<CancelRt, Option<GeneContentInfo>> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from _1 in guard(
                uniqueGeneId.GeneType is GeneType.Catlet or GeneType.Fodder,
                Error.New($"The content of the gene {uniqueGeneId} cannot be downloaded directly."))
            .ToAff()
        from genePoolClient in CreateClient().ToAff()
        from geneInfo in FetchGene(uniqueGeneId, geneHash)
        from contentInfo in geneInfo
            .Map(gi => GetGeneContent(uniqueGeneId, geneHash, gi))
            .Sequence()
        select contentInfo;
    
    

    private Aff<CancelRt, GeneContentInfo> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        RepositoryGeneInfo geneInfo) =>
        from originalSize in Optional(geneInfo.Manifest.OriginalSize)
            .ToAff(Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash}) does not contain the original size."))
        from size in Optional(geneInfo.Manifest.Size)
            .ToAff(Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash}) does not contain the size."))
        from format in Optional(geneInfo.Manifest.Format)
            .ToAff(Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash}) does not contain the format."))
        from _1 in guard(originalSize <= DirectDownloadMaxSize && size <= DirectDownloadMaxSize,
            Error.New($"The gene {uniqueGeneId} ({geneHash}) is too big for a direct download."))
        from geneParts in GeneManifestUtils.GetParts(geneInfo.Manifest)
            .ToAff()
            .MapFail(e => Error.New($"The manifest of the gene {uniqueGeneId} ({geneHash}) contains invalid parts.", e))
        from genePartHash in geneParts.Match(
            Empty: () => FailEff<GenePartHash>(Error.New($"No gene part information is available for gene {uniqueGeneId} ({geneHash}).")),
            Head: SuccessEff,
            Tail: _ => FailEff<GenePartHash>(Error.New($"The gene {uniqueGeneId} has multiple parts. Only genes with a single part can be downloaded directly.")))
        from genePartUri in geneInfo.DownloadUris.Find(genePartHash)
            .ToEff(Error.New($"The download information for the gene {uniqueGeneId} is incomplete."))
        from content in Aff<CancelRt, byte[]>(async rt =>
        {
            await using var memoryStream = new MemoryStream();
            await FetchGenePart(memoryStream, genePartUri, genePartHash, rt.CancellationToken);

            return memoryStream.ToArray();
        })
        select new GeneContentInfo(uniqueGeneId, geneHash, content, (int)originalSize, format);

    private static bool IsNotFoundError(Error error) =>
        error.Exception.Case is GenepoolClientException { StatusCode: HttpStatusCode.NotFound };
}
