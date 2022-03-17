using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

internal interface ILocalImageSource: IImageSource
{
    Task<Either<PowershellFailure, Unit>> MergeArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo,
        Func<string, Task<Unit>> reportProgress, CancellationToken cancel);
    Task<Either<PowershellFailure, ImageInfo>> ProvideFallbackImage(string path, ImageIdentifier imageIdentifier, CancellationToken cancel);

    Task<Either<PowershellFailure, ImageInfo>> CacheImage(string path, ImageInfo imageInfo, CancellationToken cancel);
    Task<Either<PowershellFailure, ArtifactInfo>> CacheArtifact(ArtifactInfo artifactInfo, ImageInfo imageInfo, CancellationToken cancel);

}