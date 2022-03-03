using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

[UsedImplicitly]
internal class LocalImageSource : ImageSourceBase, ILocalImageSource
{
    private readonly IFileSystemService _fileSystem;

    public LocalImageSource(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    private string BuildImagePath(ImageIdentifier imageIdentifier, string basePath, bool shouldExists = false)
    {
        var orgDirectory = Path.Combine(basePath, imageIdentifier.Organization);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(orgDirectory);
        var imageBaseDirectory = Path.Combine(orgDirectory, imageIdentifier.ImageId);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(imageBaseDirectory);
        var imageTagDirectory = Path.Combine(imageBaseDirectory, imageIdentifier.Tag);
        if (shouldExists) _fileSystem.EnsureDirectoryExists(imageTagDirectory);

        return imageTagDirectory;
    }

    private ImageArtifactInfo ReadImageArtifactInfo(ImageInfo imageInfo)
    {
        if (!_fileSystem.FileExists(Path.Combine(imageInfo.LocalPath, "artifacts.json")))
            return new ImageArtifactInfo { MergedArtifacts = Array.Empty<string>() };

        var json = _fileSystem.ReadText(Path.Combine(imageInfo.LocalPath, "artifacts.json"));
        var artifacts = JsonSerializer.Deserialize<ImageArtifactInfo>(json);

        if (artifacts == null || artifacts.MergedArtifacts == null)
            return new ImageArtifactInfo { MergedArtifacts = Array.Empty<string>() };

        return artifacts;
    }

    public async Task<Either<PowershellFailure, ArtifactInfo>> RetrieveArtifact(ImageInfo imageInfo, string artifact)
    {

        return await ParseArtifactName(artifact).BindAsync(async parsedArtifactName =>
        {
            var (hashAlgName, hash) = parsedArtifactName;

            var artifactsInfo = ReadImageArtifactInfo(imageInfo);
            if (artifactsInfo.MergedArtifacts.Contains(artifact))
            {
                return new ArtifactInfo(imageInfo.Id, hash, hashAlgName, null,null, true);
            }

            var messageName = $"{imageInfo.Id.Name}/{hash[..12]}";

            var artifactPath = Path.Combine(imageInfo.LocalPath, hash);
            var cachedArtifactManifestFile = Path.Combine(artifactPath, "manifest.json");

            if (!_fileSystem.FileExists(cachedArtifactManifestFile))
                return new PowershellFailure { Message = $"artifact '{messageName}' not available on local store" };


            return await Prelude.TryAsync(async () =>
                {
                    var hashAlg = CreateHashAlgorithm(hashAlgName);

                    var manifestJsonData = _fileSystem.ReadText(cachedArtifactManifestFile);
                    var hashString = GetHashString(hashAlg.ComputeHash(Encoding.UTF8.GetBytes(manifestJsonData)));

                    if (hashString == hash)
                    {

                        var manifestData = JsonSerializer.Deserialize<ArtifactManifestData>(manifestJsonData);
                        return await Prelude.RightAsync<PowershellFailure, ArtifactInfo>(
                            new ArtifactInfo(imageInfo.Id, hash, hashAlgName,
                                manifestData, artifactPath, false));
                    }


                    _fileSystem.FileDelete(cachedArtifactManifestFile);

                    return new PowershellFailure
                    {
                        Message =
                            $"Failed to verify hash of artifact '{messageName}'"
                    };


                }).ToEither(ex => new PowershellFailure { Message = ex.Message })
                .Bind(e => e.ToAsync())
                .ToEither();

        });

    }

    public Task<Either<PowershellFailure, long>> RetrieveArtifactPart(ArtifactInfo artifact, string artifactPart,
        long availableSize, long totalSize,
        Func<string, Task<Unit>> reportProgress)
    {
        return ParseArtifactPartName(artifactPart).BindAsync(async parsedArtifactPartName =>
        {
            var (hashAlgName, partHash) = parsedArtifactPartName;
            var messageName = $"{artifact}/{partHash[..12]}";

            var cachedArtifactPartFile = Path.Combine(artifact.LocalPath, $"{partHash}.part");

            if (!_fileSystem.FileExists(cachedArtifactPartFile))
                return new PowershellFailure
                    { Message = $"artifact part '{messageName}' not available on local store" };

            var hashAlg = CreateHashAlgorithm(hashAlgName);
            await using (var dataStream = File.OpenRead(cachedArtifactPartFile))
            {
                await hashAlg.ComputeHashAsync(dataStream);
            }

            var hashString = GetHashString(hashAlg.Hash);


            if (hashString != partHash)
            {
                return new PowershellFailure
                {
                    Message =
                        $"Failed to verify hash of artifact part '{messageName}'"
                };
            }

            return await Prelude.RightAsync<PowershellFailure, long>(_fileSystem.GetFileSize(cachedArtifactPartFile));
        });
    }

    public string SourceName { get; set; }


    public async Task<Either<PowershellFailure, Unit>> MergeArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo,
        Func<string, Task<Unit>> reportProgress)
    {
        var imageArtifactInfo = ReadImageArtifactInfo(imageInfo);

        if (imageArtifactInfo.MergedArtifacts.Contains($"{artifactInfo.HashAlg}:{artifactInfo.Hash}") || artifactInfo.LocalPath == null)
            return Unit.Default;

        return await Prelude.TryAsync(async () =>
        {
            await reportProgress(
                $"Extracting artifact '{artifactInfo}' to image '{imageInfo.Id.Name}'...");

            var parts = artifactInfo.MetaData?.Parts ?? Array.Empty<string>();

            var streams = parts.Map(part =>
            {
                var partHash = part.Split(':').Last();
                var path = Path.Combine(artifactInfo.LocalPath, $"{partHash}.part");
                return _fileSystem.OpenRead(path);
            }).ToArray();
            try
            {
                await using var multiStream = new MultiStream(streams);
                var decompression = new ArtifactDecompression(_fileSystem);
                await decompression.Decompress(artifactInfo.MetaData,
                        multiStream, imageInfo.LocalPath);
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }

            await using var artifactsInfoStream = File.Open(Path.Combine(imageInfo.LocalPath, "artifacts.json"),
                FileMode.Create);

            imageArtifactInfo.MergedArtifacts = imageArtifactInfo.MergedArtifacts
                .Append(new[] { $"{artifactInfo.HashAlg}:{artifactInfo.Hash}" }).ToArray();
            await JsonSerializer.SerializeAsync(artifactsInfoStream, imageArtifactInfo);

            _fileSystem.DirectoryDelete(artifactInfo.LocalPath);

            return Unit.Default;

        }).ToEither(ex => new PowershellFailure { Message = ex.Message })
            .ToEither();

    }

    public Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier)
    {
        return ProvideImage(path, imageIdentifier, false);
    }


    public Task<Either<PowershellFailure, ImageInfo>> ProvideFallbackImage(string path, ImageIdentifier imageIdentifier)
    {
        return ProvideImage(path, imageIdentifier, true);
    }


    private async Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier,
        bool fallbackMode)
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
                var manifest = await JsonSerializer.DeserializeAsync<ImageManifestData>(manifestStream);

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

                await using var manifestStream = _fileSystem.OpenWrite(Path.Combine(imagePath, "manifest.json"));
                await JsonSerializer.SerializeAsync(manifestStream, imageInfo.MetaData);
                return new ImageInfo(imageInfo.Id, imagePath, imageInfo.MetaData);

            }).ToEither(ex => new PowershellFailure { Message = ex.Message })
            .ToEither();


    }

    public async Task<Either<PowershellFailure, ArtifactInfo>> CacheArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo)
    {
        if (artifactInfo.MergedWithImage)
            return artifactInfo;

        return await Prelude.TryAsync(async () =>
            {
                var artifactPath = Path.Combine(imageInfo.LocalPath, artifactInfo.Hash);
                _fileSystem.EnsureDirectoryExists(artifactPath);

                await using var manifestStream = _fileSystem.OpenWrite(Path.Combine(artifactPath, "manifest.json"));
                await JsonSerializer.SerializeAsync(manifestStream, artifactInfo.MetaData);
                return new ArtifactInfo(artifactInfo.ImageId,artifactInfo.Hash, artifactInfo.HashAlg, artifactInfo.MetaData, artifactPath, false);

            }).ToEither(ex => new PowershellFailure { Message = ex.Message })
            .ToEither();


    }

    private class ImageArtifactInfo
    {
        public string[] MergedArtifacts { get; set; }
    }
}