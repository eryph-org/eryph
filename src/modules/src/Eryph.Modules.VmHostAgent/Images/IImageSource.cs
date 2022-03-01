using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public record ImageInfo(ImageIdentifier Id, string LocalPath, ManifestData MetaData)
{
    public readonly ImageIdentifier Id = Id;
    public readonly string LocalPath = LocalPath;
    public readonly ManifestData MetaData = MetaData;
}

public class ManifestData
{
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("alias")]
    public string Alias { get; set; }

    [JsonPropertyName("artifacts")]
    public string[] Artifacts { get; set; }

}

internal interface IImageSource
{
    Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier, Func<string, Task<Unit>> reportProgress);

    Task<Either<PowershellFailure, string>> RetrieveArtifact(string artifactsFolder, ImageInfo imageInfo,
        string artifact, Func<string, Task<Unit>> reportProgress);

    public string SourceName { get; set; }
}

internal interface ILocalImageSource: IImageSource
{
    Task<Either<PowershellFailure, Unit>> MergeArtifact(string artifact, string artifactPath, ImageInfo imageInfo, Func<string, Task<Unit>> reportProgress);
    Task<Either<PowershellFailure, ImageInfo>> ProvideFallbackImage(string path, ImageIdentifier imageIdentifier, Func<string, Task<Unit>> reportProgress);

    Task<Either<PowershellFailure, ImageInfo>> CacheImage(string path, ImageInfo imageInfo);
}