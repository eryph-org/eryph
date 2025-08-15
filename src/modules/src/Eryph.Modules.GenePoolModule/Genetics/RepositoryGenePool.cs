using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Disposables;
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
using LanguageExt.Effects.Traits;
using LanguageExt.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.Modules.GenePool.Genetics;


using GenePartInfo = (GenePartHash Part, long Size);

internal class RepositoryGenePool(
    IHttpClientFactory httpClientFactory,
    ILogger log,
    IFileSystemService fileSystem,
    IGenePoolApiKeyStore keyStore,
    IApplicationInfoProvider applicationInfo,
    IHardwareIdProvider hardwareIdProvider,
    IGeneTempPathProvider geneTempPathProvider,
    GenePoolSettings genepoolSettings)
    : GenePoolBase, IGenePool
{
    private static Error UrlExpiredError(DateTimeOffset expiresAt) => (unchecked((int)0x8000_0000), "");

    private const int BufferSize = 65536;

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

    public EitherAsync<Error, GeneSetInfo> ProvideGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancel) =>
        from genePoolClient in CreateClient()
        from geneSetInfo in TryAsync(async () =>
        {
            var genesetTagClient = genePoolClient.GetGenesetTagClient(geneSetId);
            var response = await genesetTagClient.GetForDownloadAsync(cancellationToken: cancel);
            if (response is null)
                throw Error.New("The response from the gene pool API is empty.");
            
            if (response.Manifest is null)
                throw Error.New("The gene set manifest is missing in the response of the gene pool API.");

            return new GeneSetInfo(geneSetId, response.Manifest, response.Genes.ToSeq());
        }).ToEither(ex =>
        {
            log.LogDebug(ex, "Failed to provide geneset {geneset} from gene pool {genepool}", geneSetId,
                PoolName);
            return Error.New(ex);
        })
        select geneSetInfo;

    public EitherAsync<Error, GeneInfo> RetrieveGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancel) =>
        from genePoolClient in CreateClient()
        from geneInfo in TryAsync(async () =>
        {
            var geneClient = genePoolClient.GetGeneClient(uniqueGeneId.Id.GeneSet, geneHash.ToGene());

            // TODO This does not work. The gene pool only populates ContentUri but not Content

            var response = await geneClient.GetAsync(cancellationToken: cancel);
            if (response is null)
                throw Error.New("The response from the gene pool API is empty.");
            
            if (response.Manifest is null)
                throw Error.New("The gene manifest is missing in the response of the gene pool API.");

            var downloadEntry = new GetGeneDownloadResponse(
                geneHash.Hash,
                response.Manifest,
                response.Content?.Content,
                response.DownloadUris,
                response.DownloadExpires.GetValueOrDefault());

        return new GeneInfo(uniqueGeneId, geneHash, downloadEntry.Manifest,
                downloadEntry.DownloadUris, downloadEntry.DownloadExpires, false);

        }).ToEither(ex =>
        {
            log.LogDebug(ex, "Failed to provide gene '{Gene}' from gene pool {Source}", uniqueGeneId, PoolName);
            return Error.New($"Failed to provide gene '{uniqueGeneId}' from gene pool {PoolName}", ex);
        })
        select geneInfo;

    private Aff<CancelRt, GenePartsInfo> DownloadGene2(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsInfo partsInfo,
        string downloadPath) =>
        from repositoryGeneInfo in FetchGene(uniqueGeneId, geneHash)
        from validGeneInfo in repositoryGeneInfo
            .ToAff()
        let manifestPath = GenePoolPaths.GetTempGeneManifestPath(downloadPath, geneHash)
        from _  in Aff<CancelRt, Unit>(async rt =>
        {
            var json = JsonSerializer.Serialize(validGeneInfo.Manifest, GeneModelDefaults.SerializerOptions);
            await fileSystem.WriteAllTextAsync(manifestPath, json, rt.CancellationToken);
            return unit;
        })
        
        let missingParts = partsInfo.Parts.Except(partsInfo.ExistingParts.Keys)
        from result in missingParts.Fold(
            SuccessAff<CancelRt, HashMap<GenePartHash, long>>(partsInfo.ExistingParts),
            (s, missingPart) => from validState in s
                                from r in DownloadGenePart2(uniqueGeneId, geneHash, missingPart, validGeneInfo, downloadPath)
                                select r.Match(
                                        Some: r2 => validState.Add(r2.Part, r2.Size),
                                        None: validState))
        select new GenePartsInfo(uniqueGeneId, geneHash, partsInfo.Parts, result);

    private Aff<CancelRt, Option<(GenePartHash Part, long Size)>> DownloadGenePart2(
        UniqueGeneIdentifier geneId,
        GeneHash geneHash,
        GenePartHash genePartHash,
        RepositoryGeneInfo repositoryGeneInfo,
        string downloadPath) =>
        from url in repositoryGeneInfo.DownloadUris
            .Find(genePartHash)
            .ToAff(Error.New($"The gene part {genePartHash.Hash} is not available in the gene pool."))
        let path = GenePoolPaths.GetTempGenePartPath(downloadPath, geneHash, genePartHash)
        from size in Aff<CancelRt, long>(async rt =>
            {
                try
                {
                    log.LogTrace("Downloading gene part {GenePart} from {Url}", genePartHash, url);
                    await using var fileStream = fileSystem.OpenWrite(path);
                    await FetchGenePart(fileStream, url, genePartHash, rt.CancellationToken);
                    return fileStream.Length;
                }
                catch (Exception ex)
                {
                    log.LogInformation(ex, "Failed to download gene part {GenePart} from {Url}", genePartHash, url);
                    fileSystem.FileDelete(path);
                    throw;
                }
            })
            .Map(Some).IfFail(Option<long>.None)
        select Option<(GenePartHash Part, long Size)>.None;
        //select size.Map(s => (Part: genePartHash, Size: size));

    public EitherAsync<Error, long> RetrieveGenePart(
        GeneInfo geneInfo,
        string genePartHash,
        string genePartPath,
        long availableSize,
        long totalSize,
        Func<string, int, Task<Unit>> reportProgress,
        Stopwatch stopwatch,
        CancellationToken cancel) =>
        from parsedPartHash in ParseGenePartHash(genePartHash).ToAsync()
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

            var gene = genePoolClient.GetGeneClient(geneInfo.Id.Id.GeneSet, geneInfo.Hash.ToGene());
            var response = await gene.GetAsync(cancellationToken: cancel)
                           ?? throw new InvalidDataException("empty response from gene api");
            urlEntry = response.DownloadUris?.FirstOrDefault(x => x.Part == genePartHash);

            if (urlEntry == null)
                throw new InvalidDataException(
                    $"Could not find gene part '{geneInfo.Id}/{parsedPartHash.Hash}' on {PoolName}.");

            return urlEntry.DownloadUri;

        }).ToEither()
        from fileSize in TryAsync(async () =>
        {
            var (hashAlgName, partHash) = parsedPartHash;
            var messageName = $"{geneInfo}/{partHash[..12]}";

            log.LogTrace("gene {Gene}, part {GenePart} url: {Url}",
                geneInfo, genePartHash, genePartUrl);

            using var httpClient = httpClientFactory.CreateClient(GenePoolConstants.PartClientName);
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

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancel);
            using var hashAlg = CreateHashAlgorithm(hashAlgName);
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var tempFileStream = fileSystem.OpenWrite(genePartPath))
            {
                await using var progressStream = new ProgressStream(
                    tempFileStream,
                    TimeSpan.FromSeconds(10), 
                    async (progress, _) =>
                    {
                        var totalMb = Math.Round(totalSize / 1024d / 1024d, 0);
                        var percent = Math.Round((availableSize + progress) / (double)totalSize, 3);
                        var totalReadMb = Math.Round((availableSize + progress) / 1024d / 1024d, 0);
                        var percentInt = Convert.ToInt32(percent * 100d);

                        log.LogTrace("Pulling {Gene} ({TotalReadMib} MiB / {TotalMib} MiB) => {Percent:P1} completed",
                            geneInfo.Id, totalReadMb, totalMb, percent);
                        await reportProgress(
                            $"Pulling {geneInfo.Id} ({totalReadMb} MiB / {totalMb} MiB) => {percent:P1} completed",
                            percentInt);
                    });
                await using var cryptoStream = new CryptoStream(progressStream, hashAlg, CryptoStreamMode.Write);
                await CopyToAsync(responseStream, cryptoStream, cancel);
                await cryptoStream.FlushFinalBlockAsync(cancel);
            }

            var hashString = GetHashString(hashAlg.Hash);
            log.LogTrace("gene part {Part} hash: {HashString}", messageName, hashString);

            if (hashString == partHash)
                return fileSystem.GetFileSize(genePartPath);
            
            log.LogInformation("gene part '{Part}' hash mismatch. Actual hash: {HashString}",
                messageName, hashString);

            fileSystem.FileDelete(genePartPath);
            throw new HashVerificationException($"Failed to verify hash of gene part '{messageName}'");
        }).ToEither()
        select fileSize;

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

    private EitherAsync<Error, RepositoryGeneInfo> FetchGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from genePoolClient in CreateClient()
        from response in TryAsync(async () =>
        {
            var geneClient = genePoolClient.GetGeneClient(uniqueGeneId.Id.GeneSet, geneHash.ToGene());
            var response = await geneClient.GetAsync(cancellationToken: cancellationToken);
            return Optional(response);
        }).ToEither()
        from validResponse in response.ToEitherAsync(
            Error.New("The response from the gene pool API is empty."))
        from manifest in Optional(validResponse.Manifest).ToEitherAsync(
            Error.New("The gene manifest is missing in the response of the gene pool API."))
        let manifestHash = GeneManifestUtils.ComputeHash(manifest)
        from _ in guard(manifestHash == geneHash,
            Error.New($"The gene manifest hash {manifestHash} does not match the requested gene hash {geneHash}."))
        from downloadExpiresAt in Optional(validResponse.DownloadExpires)
            .ToEitherAsync(Error.New("The download expiration time is missing in the response of the gene pool API."))
        from downloadUris in validResponse.DownloadUris.ToSeq()
            .Map(u => from partHash in GenePartHash.NewEither(u.Part)
                      select (partHash, u.DownloadUri))
            .Sequence()
            .ToAsync()
            .MapLeft(e => Error.New($"Some of the download URIs returned by the gene pool API are invalid.", e))
        select new RepositoryGeneInfo(manifest, downloadUris.ToHashMap(), downloadExpiresAt);

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
            .Catch(e => IsUnexpectedHttpClientError(e), SuccessAff<Option<GetGeneResponse>>(None))
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

    private static bool IsUnexpectedHttpClientError(Error error) =>
        error.Exception
            .Map(ex => ex is GenepoolClientException
            {
                StatusCode: >= HttpStatusCode.BadRequest
                and < HttpStatusCode.InternalServerError
                and not HttpStatusCode.NotFound
            })
            .IfNone(false);

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
            .Catch(e => IsUnexpectedHttpClientError(e), SuccessAff<Option<GenesetTagDownloadResponse>>(None))
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

    public EitherAsync<Error, GeneContentInfo> RetrieveGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from _1 in guard(uniqueGeneId.GeneType is GeneType.Catlet or GeneType.Fodder,
            Error.New($"The content of the gene {uniqueGeneId} cannot be downloaded directly."))
        from genePoolClient in CreateClient()
        from geneResponse in FetchGene(uniqueGeneId, geneHash, cancellationToken)
        from _2 in guard(geneResponse.Manifest!.OriginalSize! <= int.MaxValue && geneResponse.Manifest!.Size! <= int.MaxValue,
            Error.New("The size of the gene is too big."))
        let originalSize = (int)geneResponse.Manifest!.OriginalSize!
        let size = (int)geneResponse.Manifest!.Size!
        from geneParts in GeneManifestUtils.GetParts(geneResponse.Manifest!)
            .MapLeft(e => Error.New($"The manifest of the gene {uniqueGeneId} is invalid., e"))
            .ToAsync()
        from genePartHash in geneParts.Match<EitherAsync<Error, GenePartHash>>(
            Empty: () => Error.New($"No gene part information is available for gene {uniqueGeneId}"),
            Head: gp => gp,
            Tail: _ => Error.New($"The gene {uniqueGeneId} has multiple parts. Only genes with a single part can be downloaded directly."))
        from genePartUri in geneResponse.DownloadUris.Find(genePartHash)
            .ToEitherAsync(Error.New($"The download information for the gene {uniqueGeneId} is incomplete."))
        from content in TryAsync(async () =>
        {
            await using var memoryStream = new MemoryStream();
            await FetchGenePart(memoryStream, genePartUri, genePartHash, cancellationToken);

            return memoryStream.ToArray();
        }).ToEither()
        select new GeneContentInfo(uniqueGeneId, geneHash, content, originalSize, geneResponse.Manifest!.Format!);


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
    
    private const int DirectDownloadMaxSize = 5 * 1024 * 1024; // 100 MB

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


    public Aff<CancelRt, Option<GenePartsInfo>> DownloadGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        GenePartsInfo geneParts)
    {
        throw new NotImplementedException();
    }
}
