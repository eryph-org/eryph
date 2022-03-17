using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

internal interface IImageSource
{
    Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageIdentifier, CancellationToken cancel);

    Task<Either<PowershellFailure, ArtifactInfo>> RetrieveArtifact(ImageInfo imageInfo, string artifact, CancellationToken cancel);

    Task<Either<PowershellFailure, long>> RetrieveArtifactPart(ArtifactInfo artifact, string artifactPart, long availableSize, long totalSize, Func<string, Task<Unit>> reportProgress, CancellationToken cancel);

    public string SourceName { get; set; }
}