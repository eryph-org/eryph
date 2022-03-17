using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Images;

internal class RepositoryImageSource : ImageSourceBase, IImageSource
{

    public string SourceName { get; set; }
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _log;
    private readonly IFileSystemService _fileSystem;

    public RepositoryImageSource(IHttpClientFactory httpClientFactory, ILogger log, IFileSystemService fileSystem)
    {
        _httpClientFactory = httpClientFactory;
        _log = log;
        _fileSystem = fileSystem;
    }

    public Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier, CancellationToken cancel)
    {

        return Prelude.TryAsync(async () =>
            {
                var manifestUrl =
                    $"{imageIdentifier.Organization}/{imageIdentifier.ImageId}/{imageIdentifier.Tag}/manifest.json";
                using var httpClient = _httpClientFactory.CreateClient(SourceName);

                var response = await httpClient.GetAsync(manifestUrl, cancel);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new PowershellFailure
                        { Message = $"Could not find image '{imageIdentifier.Name}' on {SourceName}." };

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return new PowershellFailure
                    {
                        Message = $"Failed to connect to {SourceName}. Received a {response.StatusCode} HTTP response."
                    };
                }

                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
                if (contentLength == 0)
                    return new PowershellFailure
                        { Message = $"Could not find image '{imageIdentifier.Name}' on {SourceName}." };

                var manifest = ReadImageManifest(await response.Content.ReadAsStringAsync(cancel));

                if (manifest?.Image != imageIdentifier.Name)
                {
                    return new PowershellFailure { Message = $"Invalid manifest for image '{imageIdentifier.Name}'." };
                }

                return await Prelude.RightAsync<PowershellFailure, ImageInfo>(new ImageInfo(imageIdentifier, "",
                    manifest));
            }).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide image {image} from source {source}", imageIdentifier.Name,
                    SourceName);
                return new PowershellFailure { Message = ex.Message };
            })
            .Bind(e => e.ToAsync())
            .ToEither();
    }

    public Task<Either<PowershellFailure, ArtifactInfo>> RetrieveArtifact(ImageInfo imageInfo, string artifact, CancellationToken cancel)
    {
        return Prelude.TryAsync(() =>
                ParseArtifactName(artifact).BindAsync(async parsedArtifactId =>
                    {
                        var (hashAlgName, artifactHash) = parsedArtifactId;
                        var messageName = $"{imageInfo.Id.Organization}/{artifactHash[..12]}";

                        var artifactUrl = $"{imageInfo.Id.Organization}/{imageInfo.Id.ImageId}/{imageInfo.Id.Tag}/{artifactHash}/manifest.json";
                        _log.LogTrace("artifact {artifact} manifest url: {url}", messageName, artifactUrl);

                        using var httpClient = _httpClientFactory.CreateClient(SourceName);
                        var response = await httpClient.GetAsync(artifactUrl, cancel);

                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return new PowershellFailure
                            {
                                Message =
                                    $"Could not find artifact '{imageInfo.Id.Organization}/{artifactHash[..12]}' on {SourceName}."
                            };

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            return new PowershellFailure
                            {
                                Message =
                                    $"Failed to connect to {SourceName}. Received a {response.StatusCode} HTTP response."
                            };
                        }

                        var manifestContent = await response.Content.ReadAsStringAsync(cancel);
                        var hash = CreateHashAlgorithm(hashAlgName);
                        var hashString = GetHashString(hash.ComputeHash(Encoding.UTF8.GetBytes(manifestContent)));

                        if (hashString != artifactHash)
                            return new PowershellFailure
                                { Message = $"Failed to validate integrity of artifact '{messageName}'." };

                        var manifest = ReadArtifactManifest(manifestContent);

                        return await Prelude.RightAsync<PowershellFailure, ArtifactInfo>(
                            new ArtifactInfo(imageInfo.Id, artifactHash, hashAlgName, manifest,null, false));
                    }
                )).ToEither(ex =>
            {
                _log.LogDebug(ex, "Failed to provide artifact '{organization}/{artifact}' from source {source}",
                    imageInfo.Id.Organization, artifact, SourceName);
                return new PowershellFailure { Message = ex.Message };
            })
            .Bind(e => e.ToAsync())
            .ToEither();
    }

    public Task<Either<PowershellFailure, long>> RetrieveArtifactPart(ArtifactInfo artifact, string artifactPart,
        long availableSize, long totalSize,
        Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
    {

        return ParseArtifactPartName(artifactPart).BindAsync(async parsedPartName =>
        {
            var (hashAlgName, partHash) = parsedPartName;

            var messageName = $"{artifact}/{partHash[..12]}";

            var artifactPartUrl =
                $"{artifact.ImageId.Organization}/{artifact.ImageId.ImageId}/{artifact.ImageId.Tag}/{artifact.Hash}/{partHash}.part";
            _log.LogTrace("artifact part {artifact}, part {artifactPart} url: {url}", artifact,
                artifactPart, artifactPartUrl);

            using var httpClient = _httpClientFactory.CreateClient(SourceName);
            var response = await httpClient.GetAsync(artifactPartUrl, HttpCompletionOption.ResponseHeadersRead, cancel);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new PowershellFailure
                    { Message = $"Could not find artifact part '{messageName}' on {SourceName}." };

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new PowershellFailure
                {
                    Message = $"Failed to connect to eryph hub. Received a {response.StatusCode} HTTP response."
                };
            }

            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
            _log.LogTrace("artifact part {artifact}/{part} content length: {contentLength}", artifact, partHash, contentLength);

            if (contentLength == 0)
                return new PowershellFailure
                    { Message = $"Could not find artifact part '{messageName}' on {SourceName}." };

            var partFile = Path.Combine(artifact.LocalPath, $"{partHash}.part");

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancel);
            var hashAlg = CreateHashAlgorithm(hashAlgName);
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var tempFileStream = _fileSystem.OpenWrite(partFile))
            {

                var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
                await CopyToAsync(cryptoStream, tempFileStream, reportProgress,
                    $"artifact '{artifact}' from source '{SourceName}'",
                    availableSize, totalSize, cancel: cancel);

            }

            var hashString = GetHashString(hashAlg.Hash);
            _log.LogTrace("artifact part {part} hash: {hashString}", messageName, hashString);

            if (hashString != partHash)
            {
                _log.LogInformation("artifact part '{part}' hash mismatch. Actual hash: {hashString}",
                    messageName,
                    hashString);

                _fileSystem.FileDelete(partFile);
                return new PowershellFailure
                    { Message = $"Failed to verify hash of artifact part '{messageName}'" };
            }

            return await Prelude.RightAsync<PowershellFailure, long>(_fileSystem.GetFileSize(partFile)).ToEither();


        });

    }



    private async Task CopyToAsync(Stream source, Stream destination, Func<string, Task<Unit>> reportProgress, string name, long availableSize, long totalSize, int bufferSize = 65536, CancellationToken cancel = default)
    {
        var buffer = new byte[bufferSize];
        int bytesRead;
        long totalRead = 0;
        var totalMb = totalSize / 1024d / 1024d;
        
        var lastReport = DateTime.Now - TimeSpan.FromSeconds(10-3); //send first message after 3 seconds instead of 10

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
        {
            cancel.ThrowIfCancellationRequested();

            await destination.WriteAsync(buffer, 0, bytesRead, cancel);
            totalRead += bytesRead;

            var timeSinceLastReport = DateTime.Now - lastReport;
            if(timeSinceLastReport.TotalSeconds <= 10 || totalMb == 0)
                continue;

            var totalReadMb = Math.Round((availableSize+ totalRead) / 1024d / 1024d, 0);
            var percent = totalReadMb / totalMb;

            var progressMessage = $"Pulling {name} ({totalReadMb:N0} MB / {totalMb:N0} MB) => {percent:P0} completed";
            _log.LogTrace(progressMessage);
            await reportProgress(progressMessage);
            lastReport = DateTime.Now;

        }
    }

}