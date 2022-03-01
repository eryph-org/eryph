using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

internal class LocalImageSource : ImageSourceBase, ILocalImageSource
{
    private bool _keepArtifacts = true;

    public LocalImageSource()
    {
        
    }

    private static string BuildImagePath(ImageIdentifier imageIdentifier, string basePath, bool shouldExists = false)
    {
        var orgDirectory = Path.Combine(basePath, imageIdentifier.Organization);
        if (shouldExists && !Directory.Exists(orgDirectory)) Directory.CreateDirectory(orgDirectory);
        var imageBaseDirectory = Path.Combine(orgDirectory, imageIdentifier.ImageId);
        if (shouldExists && !Directory.Exists(imageBaseDirectory)) Directory.CreateDirectory(imageBaseDirectory);
        var imageTagDirectory = Path.Combine(imageBaseDirectory, imageIdentifier.Tag);
        if (shouldExists && !Directory.Exists(imageTagDirectory)) Directory.CreateDirectory(imageTagDirectory);

        return imageTagDirectory;
    }

    private static async Task<ArtifactInfo> ReadArtifactInfo(ImageInfo imageInfo)
    {
        if (!File.Exists(Path.Combine(imageInfo.LocalPath, "artifacts.json")))
            return new ArtifactInfo{MergedArtifacts = Array.Empty<string>()};

        await using var stream = File.OpenRead(Path.Combine(imageInfo.LocalPath, "artifacts.json"));
        var artifacts = await JsonSerializer.DeserializeAsync<ArtifactInfo>(stream);

        if(artifacts == null || artifacts.MergedArtifacts == null)
            return new ArtifactInfo { MergedArtifacts = Array.Empty<string>() };

        return artifacts;
    }

    public async Task<Either<PowershellFailure, string>> RetrieveArtifact(string artifactsFolder, ImageInfo imageInfo, string artifact, Func<string, Task<Unit>> reportProgress)
    {

        var artifactsInfo = await ReadArtifactInfo(imageInfo);
        if (artifactsInfo.MergedArtifacts.Contains(artifact))
            return "merged";

        return await ParseArtifactName(artifact).BindAsync(async artifactId =>
        {

            var cacheArtifactFile = Path.Combine(artifactsFolder, $"{artifactId[..12]}.zip");

            if(!File.Exists(cacheArtifactFile))
                return new PowershellFailure { Message = $"artifact '{imageInfo.Id.Organization}/{artifactId[..12]}' not available on local store" };


            return await Prelude.TryAsync(async () =>
                {
                    var hashAlg = SHA256.Create();

                    await reportProgress(
                        $"Verifying integrity artifact '{imageInfo.Id.Organization}/{artifactId[..12]}'. This could take a while...");

                    await using (var dataStream = File.OpenRead(cacheArtifactFile))
                    {
                        await hashAlg.ComputeHashAsync(dataStream);
                    }
                    
                    var hashString = BitConverter.ToString(hashAlg.Hash!).Replace("-", string.Empty).ToLowerInvariant();

                    if (hashString == artifactId)
                        return await Prelude.RightAsync<PowershellFailure, string>(cacheArtifactFile);

                    if(!_keepArtifacts)
                        File.Delete(cacheArtifactFile);

                    return new PowershellFailure
                    {
                        Message =
                            $"Failed to verify hash of artifact '{imageInfo.Id.Organization}/{artifactId[..12]}'"
                    };


                }).ToEither(ex => new PowershellFailure { Message = ex.Message })
                .Bind(e => e.ToAsync())
                .ToEither();

        });

    }

    public string SourceName { get; set; }


    public async Task<Either<PowershellFailure, Unit>> MergeArtifact(string artifact, string artifactPath, ImageInfo imageInfo, Func<string, Task<Unit>> reportProgress)
    {
        var artifactsInfo = await ReadArtifactInfo(imageInfo);

        if (artifactsInfo.MergedArtifacts.Contains(artifact))
            return Unit.Default;


        return await Prelude.TryAsync(() =>
            {
                return ParseArtifactName(artifact).MapAsync(async artifactId =>
                {
                    await using var artifactsInfoStream = File.Open(Path.Combine(imageInfo.LocalPath, "artifacts.json"),
                        FileMode.Create);
                    await reportProgress(
                        $"Extracting artifact '{imageInfo.Id.Organization}/{artifactId[..12]}' to image '{imageInfo.Id.Name}'...");

                    ZipFile.ExtractToDirectory(artifactPath, imageInfo.LocalPath);

                    artifactsInfo.MergedArtifacts = artifactsInfo.MergedArtifacts.Append(new[] { artifact }).ToArray();
                    await JsonSerializer.SerializeAsync(artifactsInfoStream, artifactsInfo);

                    if(!_keepArtifacts)
                        File.Delete(artifactPath);

                    return Unit.Default;
                });
            })

            .ToEither(ex => new PowershellFailure { Message = ex.Message })
            .Bind(e => e.ToAsync())
            .ToEither();



    }

    public Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier, Func<string, Task<Unit>> reportProgress)
    {
        return ProvideImage(path, imageIdentifier, reportProgress, false);
    }


    public Task<Either<PowershellFailure, ImageInfo>> ProvideFallbackImage(string path, ImageIdentifier imageIdentifier, Func<string, Task<Unit>> reportProgress)
    {
        return ProvideImage(path, imageIdentifier, reportProgress, true);
    }


    private async Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier,
        Func<string, Task<Unit>> reportProgress, bool fallbackMode)
    {
        if (!fallbackMode && imageIdentifier.Tag == "latest")
            return new PowershellFailure { Message = "latest image version will be look up first on remote sources." };

        return await Prelude.TryAsync(async () =>
            {
                var imagePath = BuildImagePath(imageIdentifier, path);
                if (!File.Exists(Path.Combine(imagePath, "manifest.json")))
                    return await Prelude.LeftAsync<PowershellFailure, ImageInfo>(new PowershellFailure
                        { Message = $"Image '{imageIdentifier.Name}' not found in local store." }).ToEither();

                await using var manifestStream = File.OpenRead(Path.Combine(imagePath, "manifest.json"));
                var manifest = await JsonSerializer.DeserializeAsync<ManifestData>(manifestStream);

                return await Prelude
                    .RightAsync<PowershellFailure, ImageInfo>(new ImageInfo(imageIdentifier, imagePath, manifest))
                    .ToEither();

            })
            .ToEither(ex => new PowershellFailure { Message = ex.Message })
            .Bind(e => e.ToAsync())
            .ToEither();

    }

    public Task<Either<PowershellFailure, ImageInfo>> CacheImage(string path, ImageInfo imageInfo)
    {
        return Prelude.TryAsync(async () =>
            {
                var imagePath = BuildImagePath(imageInfo.Id, path, true);

                await using var manifestStream = File.Create(Path.Combine(imagePath, "manifest.json"));
                await JsonSerializer.SerializeAsync(manifestStream, imageInfo.MetaData);
                return new ImageInfo(imageInfo.Id, imagePath, imageInfo.MetaData);

            }).ToEither(ex => new PowershellFailure{Message = ex.Message})
            .ToEither();

        
    }

    private class ArtifactInfo
    {
        public string[] MergedArtifacts { get; set; }
    }
}