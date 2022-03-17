using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Images
{
    internal class LocalFirstImageProvider : IImageProvider
    {
        private readonly IImageSourceFactory _sourceFactory;
        private readonly ILogger _log;

        public LocalFirstImageProvider(IImageSourceFactory sourceFactory, ILogger log)
        {
            _sourceFactory = sourceFactory;
            _log = log;
        }

        public Task<Either<PowershellFailure, string>> ProvideImage(string imageName, Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();

            var path = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, "Images");

            return ImageIdentifier.Parse(imageName).MatchAsync(
                Left: l => l,
                RightAsync: imageId =>
                {
                    
                    return ProvideImage(path, imageId, reportProgress, Array.Empty<string>(), cancel)
                        .MapAsync(i =>
                        {
                            if (i.Id.Name != imageName)
                                reportProgress($"Resolved image '{imageName}' as '{i.Id.Name}'");
                            return i;
                        })
                        .BindAsync(imageInfo => EnsureArtifacts(imageInfo, reportProgress, cancel))
                        .MapAsync(imageInfo => imageInfo.Id.Name);

                }

            );
            
        }


        private async Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageId,
            Func<string, Task<Unit>> reportProgress, IEnumerable<string> previousRefs, CancellationToken cancel)
        {
            var imageRefs = previousRefs as string[] ?? previousRefs.ToArray();
            if (imageRefs.Contains(imageId.Name))
            {
                var imageNames = string.Join('?', imageRefs.Append(new[] { imageId.Name })).Replace("?", " => ");
                return new PowershellFailure
                {
                    Message =
                        $"Detected circular image references. Contact organization '{imageId.Organization}' to resolve invalid image references of '{imageNames}'"
                };
            }

            var localSource = _sourceFactory.CreateLocal();

            return await localSource.ProvideImage(path, imageId, cancel) //found locally? (will not resolve 'latest' tag)
                .ToAsync()
                .BindLeft(l =>
                {
                    _log.LogDebug("Failed to find image on local source. Local result: {message}", l.Message);

                    return ProvideImageFromRemote(path, imageId, cancel).ToAsync();
                }) // fetch remote image
                .BindLeft(l =>
                {
                    _log.LogDebug("Failed to find image on remote sources. Remote sources result: {message}", l.Message);
                    return localSource.ProvideFallbackImage(path, imageId, cancel).ToAsync();
                }) //local fallback (will resolve 'latest' tag)
                .MapLeft(l =>
                {
                    _log.LogInformation("Failed to find image on any source. Local fallback result: {message}", l.Message);
                    return new PowershellFailure
                            { Message = $"could not find image '{imageId.Name}' on any source." };
                })
                .ToEither()
                .BindAsync(imageInfo => localSource.CacheImage(path,imageInfo, cancel)) // cache anything received in local store
                .BindAsync(async r =>
                    {
                        if (!string.IsNullOrWhiteSpace(r.MetaData.Reference))
                        {
                            await reportProgress($"Image '{r.Id.Name}' references '{r.MetaData.Reference}'");
                            //resolve image reference
                            return await ImageIdentifier.Parse(r.MetaData.Reference)
                                .BindAsync(aliasId => ProvideImage(path, aliasId, reportProgress,
                                    imageRefs.Append(new[] { imageId.Name }), cancel));
                        }

                        return await Prelude.RightAsync<PowershellFailure, ImageInfo>(r).ToEither();

                    });


        }


        private async Task<Either<PowershellFailure, ImageInfo>> ProvideImageFromRemote(string path, ImageIdentifier imageId, CancellationToken cancel)
        {
            _log.LogDebug("Trying to find image {image} on remote sources", imageId.Name);
            foreach (var sourceName in _sourceFactory.RemoteSources)
            {
                cancel.ThrowIfCancellationRequested();

                var imageSource = _sourceFactory.CreateNew(sourceName);
                var result = await imageSource.ProvideImage(path, imageId, cancel);

                result.IfLeft(l =>
                {
                    _log.LogInformation("Failed to lookup image {image} on source {source}. Message: {message}", imageId.Name,
                        sourceName, l.Message);
                });

                if (result.IsRight)
                {
                    _log.LogDebug("{image} found on source {source}", imageId.Name, sourceName);

                    return result;
                }
            }

            return new PowershellFailure{ Message = $"could not find image {imageId.Name} on any source."};
        }

        private async Task<Either<PowershellFailure, ImageInfo>> EnsureArtifacts(ImageInfo imageInfo,
            Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            var localSource = _sourceFactory.CreateLocal();

            if (imageInfo.MetaData?.Artifacts == null || imageInfo.MetaData.Artifacts.Length == 0)
                return new PowershellFailure
                    { Message = $"Invalid image '{imageInfo.Id.Name}': no artifacts in image" };

            foreach (var artifact in imageInfo.MetaData.Artifacts)
            {
                cancel.ThrowIfCancellationRequested();

                var result = await localSource.RetrieveArtifact(imageInfo, artifact, cancel)
                    .ToAsync()
                    .BindLeft(l =>
                        ProvideArtifactFromRemote(imageInfo, artifact, cancel).ToAsync())
                    .Bind(artifactInfo => localSource.CacheArtifact(artifactInfo, imageInfo, cancel).ToAsync())
                    .Bind(artifactInfo => EnsureArtifactParts(artifactInfo, reportProgress, cancel).ToAsync())
                    .Bind(artifactInfo => localSource.MergeArtifact(artifactInfo, imageInfo, reportProgress, cancel).ToAsync());

                result.IfLeft(l =>
                {
                    _log.LogInformation("Failed to retrieve artifact {artifact}. Message: {message}", artifact, l.Message);
                });

                if (!result.IsRight)
                    return result.Map(_ => imageInfo);
            }

            return imageInfo;

        }

        private async Task<Either<PowershellFailure, ArtifactInfo>> EnsureArtifactParts(ArtifactInfo artifactInfo, Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {
            var localSource = _sourceFactory.CreateLocal();
            var parts = (artifactInfo.MetaData?.Parts ?? Array.Empty<string>()).ToList();
            var retries = 0;

            var partsMissingLocal = new List<string>();
            var sizeAvailableLocal = 0L;

            foreach (var artifactPart in parts.ToArray())
            {
                cancel.ThrowIfCancellationRequested();

                var res = await localSource.RetrieveArtifactPart(artifactInfo, artifactPart, sizeAvailableLocal, artifactInfo.MetaData?.Size ?? 0, reportProgress, cancel);

                res.IfRight( r =>
                {
                    sizeAvailableLocal += r;
                });

                res.IfLeft(l =>
                {
                    partsMissingLocal.Add(artifactPart);
                });

            }


            while (partsMissingLocal.Count > 0 && retries < 5)
            {
                cancel.ThrowIfCancellationRequested();

                foreach (var artifactPart in partsMissingLocal.ToArray())
                {
                    cancel.ThrowIfCancellationRequested();

                    var res = await ProvideArtifactPartFromRemote(artifactInfo, artifactPart, sizeAvailableLocal, artifactInfo.MetaData?.Size ?? 0, reportProgress, cancel);

                    res.IfRight(r =>
                    {
                        partsMissingLocal.Remove(artifactPart);
                        sizeAvailableLocal += r;
                    });
                }

                if (partsMissingLocal.Count > 0)
                {
                    await Task.Delay(2000);
                    retries++;
                }
            }

            if (partsMissingLocal.Count > 0)
            {
                return new PowershellFailure
                {
                    Message =
                        $"Failed to provide all part of artifact {artifactInfo} from sources."
                };
            }
            return artifactInfo;
        }

        private async Task<Either<PowershellFailure, ArtifactInfo>> ProvideArtifactFromRemote(ImageInfo imageInfo, string artifact, CancellationToken cancel)
        {

            foreach (var sourceName in _sourceFactory.RemoteSources)
            {
                var imageSource = _sourceFactory.CreateNew(sourceName);
                var result = await imageSource.RetrieveArtifact(imageInfo, artifact, cancel);

                result.IfLeft(l =>
                {
                    _log.LogInformation("Failed to retrieve artifact {artifact} on source {source}. Message: {message}", artifact, sourceName, l.Message);
                });

                if (result.IsRight)
                    return result;

            }

            return new PowershellFailure { Message = $"could not find artifact on any remote source." };

        }


        private async Task<Either<PowershellFailure, long>> ProvideArtifactPartFromRemote(
            ArtifactInfo artifactInfo, string artifactPart, long availableSize, long totalSize, Func<string, Task<Unit>> reportProgress, CancellationToken cancel)
        {

            foreach (var sourceName in _sourceFactory.RemoteSources)
            {
                var imageSource = _sourceFactory.CreateNew(sourceName);
                var result = await imageSource.RetrieveArtifactPart(artifactInfo, artifactPart, availableSize, totalSize, reportProgress, cancel);

                result.IfLeft(l =>
                {
                    _log.LogInformation("Failed to retrieve artifact part {artifactPart} of artifact {artifact} on source {source}. Message: {message}", artifactPart, artifactInfo, sourceName, l.Message);
                });

                if (result.IsRight)
                    return result;

            }

            return new PowershellFailure { Message = $"could not find artifact part on any remote source." };

        }
    }
}
