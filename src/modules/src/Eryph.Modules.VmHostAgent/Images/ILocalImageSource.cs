using System;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

internal interface ILocalImageSource: IImageSource
{
    Task<Either<PowershellFailure, Unit>> MergeArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo,
        Func<string, Task<Unit>> reportProgress);
    Task<Either<PowershellFailure, ImageInfo>> ProvideFallbackImage(string path, ImageIdentifier imageIdentifier);

    Task<Either<PowershellFailure, ImageInfo>> CacheImage(string path, ImageInfo imageInfo);
    Task<Either<PowershellFailure, ArtifactInfo>> CacheArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo);

}