using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

internal class RepositoryImageSource : ImageSourceBase, IImageSource
{
    public string SourceName { get; set; }
    private readonly IHttpClientFactory _httpClientFactory;

    public RepositoryImageSource(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier, Func<string, Task<Unit>> reportProgress)
    {
        var manifestUrl = $"{imageIdentifier.Organization}/{imageIdentifier.ImageId}/{imageIdentifier.Tag}/manifest.json";
        using var httpClient = _httpClientFactory.CreateClient(SourceName);

        var response = await httpClient.GetAsync(manifestUrl);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new PowershellFailure { Message = $"Could not find image '{imageIdentifier.Name}' on {SourceName}." };

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new PowershellFailure { Message = $"Failed to connect to {SourceName}. Received a {response.StatusCode} HTTP response." };
        }

        var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
        if (contentLength == 0)
            return new PowershellFailure { Message = $"Could not find image '{imageIdentifier.Name}' on {SourceName}." };

        var manifest = ReadManifest(await response.Content.ReadAsStringAsync());

        if (manifest?.Image != imageIdentifier.Name)
        {
            return new PowershellFailure { Message = $"Invalid manifest for image '{imageIdentifier.Name}'." };
        }

        return new ImageInfo(imageIdentifier, "", manifest);
    }

    public Task<Either<PowershellFailure, string>> RetrieveArtifact(string artifactsFolder, ImageInfo imageInfo, string artifact, Func<string, Task<Unit>> reportProgress)
    {

        return ParseArtifactName(artifact).BindAsync(async artifactId =>
        {

            var artifactUrl = $"{imageInfo.Id.Organization}/_a/sha256/{artifactId[..2]}/{artifactId}.zip";

            using var httpClient = _httpClientFactory.CreateClient(SourceName);
            var response = await httpClient.GetAsync(artifactUrl, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new PowershellFailure
                    { Message = $"Could not find artifact '{imageInfo.Id.Organization}/{artifactId[..12]}' on {SourceName}." };

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new PowershellFailure
                    { Message = $"Failed to connect to eryph hub. Received a {response.StatusCode} HTTP response." };
            }

            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(0);
            if (contentLength == 0)
                return new PowershellFailure
                    { Message = $"Could not find artifact '{imageInfo.Id.Organization}/{artifact[..12]}' on {SourceName}." };

            var artifactFile = Path.Combine(artifactsFolder, $"{artifactId[..12]}.zip");

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var hashAlg = SHA256.Create();
            // ReSharper disable once ConvertToUsingDeclaration
            await using (var tempFileStream = new FileStream(artifactFile, FileMode.Create, FileAccess.Write))
            {

                var cryptoStream = new CryptoStream(responseStream, hashAlg, CryptoStreamMode.Read);
                await CopyToAsync(cryptoStream, tempFileStream, reportProgress,
                    $"artifact '{imageInfo.Id.Organization}/{artifactId[..12]}'",
                    contentLength);

            }

            var hashString = BitConverter.ToString(hashAlg.Hash!).Replace("-", string.Empty).ToLowerInvariant();

            if (hashString != artifactId)
            {
                File.Delete(artifactFile);
                return new PowershellFailure
                    { Message = $"Failed to verify hash of artifact '{imageInfo.Id.Organization}/{artifactId[..12]}'" };
            }

            return await Prelude.RightAsync<PowershellFailure, string>(artifactFile).ToEither();
        });
    }


    private static async Task CopyToAsync(Stream source, Stream destination, Func<string, Task<Unit>> reportProgress, string name, long contentLength, int bufferSize = 65536)
    {
        var buffer = new byte[bufferSize];
        int bytesRead;
        long totalRead = 0;
        var totalMb = contentLength / 1024d / 1024d;
        var lastReport = DateTime.Now;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;

            var timeSinceLastReport = DateTime.Now - lastReport;
            if(timeSinceLastReport.TotalSeconds <= 10 || totalMb == 0)
                continue;

            var totalReadMb = Math.Round(totalRead / 1024d / 1024d, 0);
            var percent = totalReadMb / totalMb;


            await reportProgress($"pulling {name} ({totalReadMb:N0} MB / {totalMb:N0} MB) => {percent:P0} completed");
            lastReport = DateTime.Now;

        }
    }

}