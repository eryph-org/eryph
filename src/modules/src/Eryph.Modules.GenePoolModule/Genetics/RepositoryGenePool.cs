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
using Eryph.Core.Genetics;
using Eryph.GenePool.Client;
using Eryph.GenePool.Client.Credentials;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
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
    : GenePoolBase, IGenePool
{
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

            return new GeneSetInfo(geneSetId, response.Manifest, response.Genes ?? []);
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
            //var downloadEntry = geneSetInfo.GeneDownloadInfo.FirstOrDefault(x => x.Gene == parsedGeneId.Hash);

            //if (downloadEntry == null)
            //{
                var geneClient = genePoolClient.GetGeneClient(uniqueGeneId.Id.GeneSet, geneHash.Hash);

                // TODO This does not work. The gene pool only populates ContentUri but not Content

                var response = await geneClient.GetAsync(cancellationToken: cancel);
                if (response is null)
                    throw Error.New("The response from the gene pool API is empty.");
                
                if (response.Manifest is null)
                    throw Error.New("The gene manifest is missing in the response of the gene pool API.");

                var downloadEntry = new GetGeneDownloadResponse(
                    geneHash.Hash.Value,
                    response.Manifest,
                    response.Content?.Content,
                    response.DownloadUris,
                    response.DownloadExpires.GetValueOrDefault());
            //}

            return new GeneInfo(uniqueGeneId, geneHash, downloadEntry.Manifest,
                downloadEntry.DownloadUris, downloadEntry.DownloadExpires, false, downloadEntry.Content);

        }).ToEither(ex =>
        {
            log.LogDebug(ex, "Failed to provide gene '{Gene}' from gene pool {Source}", uniqueGeneId, PoolName);
            return Error.New($"Failed to provide gene '{uniqueGeneId}' from gene pool {PoolName}", ex);
        })
        select geneInfo;

    public EitherAsync<Error, GeneContentInfo> RetrieveGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from _1 in guard(uniqueGeneId.GeneType is GeneType.Catlet or GeneType.Fodder,
            Error.New($"The content of the gene {uniqueGeneId} cannot be downloaded directly."))
        from genePoolClient in CreateClient()
        from geneResponse in RetrieveGene(uniqueGeneId, geneHash, cancellationToken)
        from _2 in guard(geneResponse.Manifest!.OriginalSize! <= int.MaxValue && geneResponse.Manifest!.Size! <= int.MaxValue,
            Error.New($"The size of the gene is too big."))
        let originalSize = (int)geneResponse.Manifest!.OriginalSize!
        let size = (int)geneResponse.Manifest!.Size!
        from geneParts in GeneManifestUtils.GetParts(geneResponse.Manifest!)
            .MapLeft(e => Error.New($"The manifest of the gene {uniqueGeneId} is invalid., e"))
            .ToAsync()
        from genePartHash in geneParts.Match<EitherAsync<Error, GenePartHash>>(
            Empty: () => Error.New($"No gene part information is available for gene {uniqueGeneId}"),
            Head: gp => gp,
            Tail: _ => Error.New($"The gene {uniqueGeneId} has multiple parts. Only genes with a single part can be downloaded directly."))
        from genePartUri in geneResponse.DownloadUris.ToSeq()
            .Find(u => GenePartHash.NewOption(u.Part) == Some(genePartHash))
            .Map(u => u.DownloadUri)
            .ToEitherAsync(Error.New($"The download information for the gene {uniqueGeneId} is incomplete."))
        from content in TryAsync(async () =>
        {
            await using var memoryStream = new MemoryStream();
            await FetchGenePart(memoryStream, genePartUri, genePartHash, cancellationToken);

            return memoryStream.ToArray();
        }).ToEither()
        select new GeneContentInfo(uniqueGeneId, geneHash, content, originalSize, geneResponse.Manifest!.Format!);

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

            var gene = genePoolClient.GetGeneClient(geneInfo.Id.Id.GeneSet, geneInfo.Hash.Hash);
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
        
        var hashAlgorithm = CreateHashAlgorithm(genePartHash);
        await using var cryptoStream = new CryptoStream(targetStream, hashAlgorithm, CryptoStreamMode.Write);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await CopyToAsync(responseStream, cryptoStream, cancellationToken);

        await cryptoStream.FlushFinalBlockAsync(cancellationToken);

        VerifyHash(genePartHash, hashAlgorithm);
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

    private HashAlgorithm CreateHashAlgorithm(GenePartHash genePartHash)
    {
        if (genePartHash.Algorithm is not "sha1")
            throw new ArgumentException(
                $"The algorithm {genePartHash.Algorithm} of the gene part hash is not supported.",
                nameof(genePartHash));

        return SHA1.Create();
    }

    private void VerifyHash(GenePartHash expected, HashAlgorithm actual)
    {
        if (expected.Algorithm is not "sha1" || actual is not SHA1)
            throw new ArgumentException(
                $"The algorithm {expected.Algorithm} of the gene part hash is not supported.",
                nameof(expected));

        if (actual.Hash is null)
            throw new HashVerificationException(
                $"The hash of the gene part has not been computed.");

        var actualSpan  = new ReadOnlySpan<byte>(actual.Hash);
        var expectedSpan = new ReadOnlySpan<byte>(Convert.FromHexString(expected.Hash.Value));

        if (actualSpan.SequenceEqual(expectedSpan))
            return;

        throw new HashVerificationException($"Failed to verify the hash of the gene part. Maybe it got corrupted during transfer.");
    }

    private CryptoStream CreateCryptoStream(Stream outputStream, GenePartHash genePartHash)
    {
        if (genePartHash.Algorithm is not "sha1")
            throw new ArgumentException(
                $"The algorithm {genePartHash.Algorithm} of the gene part hash is not supported.",
                nameof(genePartHash));

        var hashAlgorithm = SHA1.Create();
        return new CryptoStream(outputStream, hashAlgorithm, CryptoStreamMode.Write);
    }
}
