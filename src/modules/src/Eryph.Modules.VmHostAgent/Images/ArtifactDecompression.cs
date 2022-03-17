using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Joveler.Compression.XZ;

namespace Eryph.Modules.VmHostAgent.Images
{
    internal class ArtifactDecompression
    {
        private readonly IFileSystemService _fileSystemService;

        public ArtifactDecompression(IFileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
        }

        public async Task Decompress(ArtifactManifestData artifactManifest, Stream stream, string imageDirectory, CancellationToken cancel)
        {
            switch (artifactManifest.Format)
            {
                case "zip":
                    DecompressZip(artifactManifest, stream, imageDirectory);
                    break;
                case "xz":
                    await DecompressXz(artifactManifest, stream, imageDirectory, cancel);
                    break;
            }
        }

        private void DecompressZip(ArtifactManifestData artifactManifest, Stream stream, string imageDirectory)
        {
            using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

            var path = imageDirectory;
            if (artifactManifest?.Path != null)
            {
                path = Path.Combine(path, artifactManifest.Path);
                _fileSystemService.EnsureDirectoryExists(path);
            }

            zipArchive.ExtractToDirectory(path,true);

        }

        private async Task DecompressXz(ArtifactManifestData artifactManifest, Stream stream, string imageDirectory, CancellationToken cancel)
        {
            InitNativeLibrary();

            var folderName = Path.Combine(imageDirectory, artifactManifest.Path);
            _fileSystemService.EnsureDirectoryExists(folderName);

            if (artifactManifest.FileName == null)
                throw new InvalidOperationException("Missing filename for single file compressed artifact.");

            var fileName = Path.Combine(folderName, artifactManifest.FileName);
            await using var fileStream = _fileSystemService.OpenWrite(fileName);

            await using var xzs = new XZStream(stream, new XZDecompressOptions());
            await xzs.CopyToAsync(fileStream, cancel);
        }

        private static bool _nativeInitialized;

        private static void InitNativeLibrary()
        {
            if (_nativeInitialized)
                return;

            _nativeInitialized = true;

            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "liblzma.dll");
            
            if (!File.Exists(libPath))
                throw new PlatformNotSupportedException($"Unable to find native library [{libPath}].");

            XZInit.GlobalInit(libPath);
        }
    }
}
