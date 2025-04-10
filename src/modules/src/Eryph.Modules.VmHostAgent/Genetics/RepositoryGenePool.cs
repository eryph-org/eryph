﻿using System;
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
    GenepoolSettings genepoolSettings)
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
        GeneSetInfo geneSetInfo,
        UniqueGeneIdentifier uniqueGeneId,
        string geneHash,
        CancellationToken cancel) =>
        from parsedGeneId in ParseGeneHash(geneHash).ToAsync()
        from genePoolClient in CreateClient()
        from geneInfo in TryAsync(async () =>
        {
            var downloadEntry = geneSetInfo.GeneDownloadInfo.FirstOrDefault(x => x.Gene == parsedGeneId.Hash);

            if (downloadEntry == null)
            {
                var geneClient = genePoolClient.GetGeneClient(uniqueGeneId.Id.GeneSet.Value, parsedGeneId.Hash);

                var response = await geneClient.GetAsync(cancellationToken: cancel);
                if (response is null)
                    throw Error.New("The response from the gene pool API is empty.");
                
                if (response.Manifest is null)
                    throw Error.New("The gene manifest is missing in the response of the gene pool API.");

                downloadEntry = new GetGeneDownloadResponse(
                    parsedGeneId.Hash,
                    response.Manifest,
                    response.Content?.Content,
                    response.DownloadUris,
                    response.DownloadExpires.GetValueOrDefault());
            }

            return new GeneInfo(uniqueGeneId, geneHash, downloadEntry.Manifest,
                downloadEntry.DownloadUris, downloadEntry.DownloadExpires, false);

        }).ToEither(ex =>
        {
            log.LogDebug(ex, "Failed to provide gene '{Gene}' from gene pool {Source}", uniqueGeneId, PoolName);
            return Error.New($"Failed to provide gene '{uniqueGeneId}' from gene pool {PoolName}", ex);
        })
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
        from parsedGeneHash in ParseGeneHash(geneInfo.Hash).ToAsync()
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

            var gene = genePoolClient.GetGeneClient(geneInfo.Id.Id.GeneSet.Value, parsedGeneHash.Hash);
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
                await using var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
                await CopyToAsync(cryptoStream, tempFileStream, reportProgress,
                    geneInfo.Id,
                    availableSize, totalSize, stopwatch, cancel: cancel);
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

    private async Task CopyToAsync(
        Stream source,
        Stream destination,
        Func<string, int, Task<Unit>> reportProgress,
        UniqueGeneIdentifier uniqueGeneId,
        long availableSize,
        long totalSize,
        Stopwatch stopwatch,
        CancellationToken cancel = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long totalRead = 0;
            var totalMb = Math.Round(totalSize / 1024d / 1024d, 0);

            while (true)
            {
                // We are (indirectly) reading from a stream provided by the HTTP client.
                // In this case, the read operation might block indefinitely if the network
                // connection is lost. Hence, we explicitly define a reasonable timeout.
                // See https://github.com/dotnet/runtime/issues/36822.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutCts.Token);
                var bytesRead = await source.ReadAsync(new Memory<byte>(buffer), combinedCts.Token);
                combinedCts.Token.ThrowIfCancellationRequested();
                
                if (bytesRead <= 0)
                    return;

                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancel);
                totalRead += bytesRead;

                if (stopwatch.Elapsed.TotalSeconds <= 10 || totalSize == 0)
                    continue;

                var percent = Math.Round((availableSize + totalRead) / (double)totalSize, 3);
                var totalReadMb = Math.Round((availableSize + totalRead) / 1024d / 1024d, 0);
                var percentInt = Convert.ToInt32(percent * 100d);

                log.LogTrace("Pulling {Gene} ({TotalReadMib} MiB / {TotalMib} MiB) => {Percent:P1} completed",
                    uniqueGeneId, totalReadMb, totalMb, percent);
                await reportProgress(
                    $"Pulling {uniqueGeneId} ({totalReadMb} MiB / {totalMb} MiB) => {percent:P1} completed",
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
