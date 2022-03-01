using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images
{
    internal class LocalFirstImageProvider : IImageProvider
    {
        private readonly IImageSourceFactory _sourceFactory;

        public LocalFirstImageProvider(IImageSourceFactory sourceFactory)
        {
            _sourceFactory = sourceFactory;
        }

        public Task<Either<PowershellFailure, string>> ProvideImage(string path, string imageName, Func<string, Task<Unit>> reportProgress)
        {
            return ImageIdentifier.Parse(imageName).MatchAsync(
                Left: l => l,
                RightAsync: imageId =>
                {
                    
                    return ProvideImage(path, imageId, reportProgress, Array.Empty<string>())
                        .MapAsync(i =>
                        {
                            if (i.Id.Name != imageName)
                                reportProgress($"resolved image '{imageName}' as '{i.Id.Name}'");
                            return i;
                        })
                        .BindAsync(imageInfo => EnsureArtifacts(path, imageInfo, reportProgress))
                        .MapAsync(imageInfo => imageInfo.Id.Name);

                }

            );
            
        }


        private async Task<Either<PowershellFailure, ImageInfo>> ProvideImage(string path, ImageIdentifier imageId,
            Func<string, Task<Unit>> reportProgress, IEnumerable<string> previousRefs)
        {
            var imageRefs = previousRefs as string[] ?? previousRefs.ToArray();
            if (imageRefs.Contains(imageId.Name))
            {
                var imageNames = string.Join(',', imageRefs, imageRefs.Append(new[] { imageId.Name }));
                return new PowershellFailure
                {
                    Message =
                        $"Detected circular reference in image aliases. Contact organization '{imageId.Organization}' to resolve invalid image references of images '{imageNames}'"
                };
            }

            var localSource = _sourceFactory.CreateLocal();

            return await localSource.ProvideImage(path, imageId, reportProgress) //found locally? (will not resolve 'latest' tag)
                .ToAsync()
                .BindLeft(l => ProvideImageFromRemote(path, imageId, reportProgress).ToAsync()) // fetch remote image
                .BindLeft(l => localSource.ProvideFallbackImage(path, imageId, reportProgress).ToAsync()) //local fallback (will resolve 'latest' tag)
                .MapLeft(l => new PowershellFailure { Message = $"could not find image '{imageId.Name}' on any source." })
                .ToEither()
                .BindAsync(imageInfo => localSource.CacheImage(path,imageInfo)) // cache anything received in local store
                .BindAsync(r =>
                    {
                        if (!string.IsNullOrWhiteSpace(r.MetaData.Alias))
                        {
                            //resolve image alias
                            return ImageIdentifier.Parse(r.MetaData.Alias)
                                .BindAsync(aliasId => ProvideImage(path, aliasId, reportProgress,
                                    imageRefs.Append(new[] { imageId.Name })));
                        }

                        return Prelude.RightAsync<PowershellFailure, ImageInfo>(r).ToEither();

                    });


        }


        private async Task<Either<PowershellFailure, ImageInfo>> ProvideImageFromRemote(string path, ImageIdentifier imageId,
            Func<string, Task<Unit>> reportProgress)
        {
            foreach (var sourceName in _sourceFactory.RemoteSources)
            {
                var imageSource = _sourceFactory.CreateNew(sourceName);
                var result = await imageSource.ProvideImage(path, imageId, reportProgress);
                if (result.IsRight)
                    return result;

            }

            return new PowershellFailure{ Message = $"could not find image {imageId.Name} on any source."};
        }

        private async Task<Either<PowershellFailure, ImageInfo>> EnsureArtifacts(string path, ImageInfo imageInfo,
            Func<string, Task<Unit>> reportProgress)
        {
            var localSource = _sourceFactory.CreateLocal();

            if (imageInfo?.MetaData?.Artifacts == null || imageInfo.MetaData.Artifacts.Length == 0)
                return new PowershellFailure
                    { Message = $"Invalid image '{imageInfo.Id.Name}': no artifacts in image" };

            var artifactsFolder = Path.Combine(path, imageInfo.Id.Organization, "_a");
            if (!Directory.Exists(artifactsFolder))
                Directory.CreateDirectory(artifactsFolder);


            foreach (var artifact in imageInfo.MetaData.Artifacts)
            {
                var result = await localSource.RetrieveArtifact(artifactsFolder, imageInfo, artifact, reportProgress)
                    .ToAsync()
                    .BindLeft(l =>
                        ProvideArtifactFromRemote(artifactsFolder, imageInfo, artifact, reportProgress).ToAsync())
                    .Bind(artifactPath => localSource.MergeArtifact(artifact, artifactPath, imageInfo, reportProgress).ToAsync());

                if (result.IsRight)
                    return result.Map(_ => imageInfo);
            }

            return new PowershellFailure { Message = $"could not find artifact on any source." };

        }

        private async Task<Either<PowershellFailure, string>> ProvideArtifactFromRemote(string artifactsFolder, ImageInfo imageInfo,
            string artifact, Func<string, Task<Unit>> reportProgress)
        {

            foreach (var sourceName in _sourceFactory.RemoteSources)
            {
                var imageSource = _sourceFactory.CreateNew(sourceName);
                var result = await imageSource.RetrieveArtifact(artifactsFolder, imageInfo, artifact, reportProgress);
                if (result.IsRight)
                    return result;

            }

            return new PowershellFailure { Message = $"could not find artifact on any remote source." };

        }
    }
}
