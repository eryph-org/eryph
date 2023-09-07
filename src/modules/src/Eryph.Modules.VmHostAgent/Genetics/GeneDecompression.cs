using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Resources;
using Joveler.Compression.XZ;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Genetics
{
    internal class GeneDecompression
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogger _log;
        private readonly Func<string, Task<Unit>> _reportProgress;
        private readonly GeneInfo _gene;

        public GeneDecompression(IFileSystemService fileSystemService, ILogger log, Func<string, Task<Unit>> reportProgress, GeneInfo gene)
        {
            _fileSystemService = fileSystemService;
            _log = log;
            _reportProgress = reportProgress;
            _gene = gene;
        }

        public async Task Decompress(GeneManifestData geneManifest, Stream stream, string genesetDirectory, CancellationToken cancel)
        {
            switch (geneManifest.Format)
            {
                case "gz":
                    await DecompressGZip(geneManifest, stream, genesetDirectory, cancel);
                    break;
                case "xz":
                    await DecompressXz(geneManifest, stream, genesetDirectory, cancel);
                    break;
            }
        }

        private async Task DecompressGZip(GeneManifestData geneManifest, Stream stream, string genesetDirectory, CancellationToken cancellationToken)
        {

            var folderName = genesetDirectory;
            var genePath = GetGenePath(geneManifest.Type);
            if (!string.IsNullOrWhiteSpace(genePath))
            {
                folderName = Path.Combine(folderName, genePath);
            }

            _fileSystemService.EnsureDirectoryExists(folderName);

            if (geneManifest.FileName == null)
                throw new InvalidOperationException("Missing filename for compressed gene.");

            var fileName = Path.Combine(folderName, geneManifest.FileName);
            await using var fileStream = _fileSystemService.OpenWrite(fileName);

            await using var xzs = new GZipStream(stream,CompressionMode.Decompress);
            await DecompressStream(xzs, fileStream, geneManifest.OriginalSize.GetValueOrDefault(), cancellationToken);

        }

        private async Task DecompressStream(Stream source, Stream destination, long totalSize, CancellationToken cancel = default)
        {
            var bufferSize = GetCopyBufferSize(source);
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            int bytesRead;
            long totalRead = 0;
            var totalMb = totalSize / 1024d / 1024d;

            var stopWatch = Stopwatch.StartNew();
            while ((bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancel)) > 0)
            {
                cancel.ThrowIfCancellationRequested();

                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancel);
                totalRead += bytesRead;

                if (stopWatch.Elapsed.TotalSeconds <= 10 || totalMb == 0)
                    continue;

                var totalReadMb = Math.Round(totalRead / 1024d / 1024d, 0);
                var percent = totalReadMb / totalMb;

                var progressMessage = $"Extracting {_gene} ({totalReadMb:F} MB / {totalMb:F} MB) => {percent:P0} completed";
                _log.LogTrace("Extracting {gene} ({totalReadMb} MB / {totalMb} MB) => {percent} completed",
                    _gene, totalReadMb, totalMb, percent);
                await _reportProgress(progressMessage);
                stopWatch.Restart();

            }
        }

        //License: Licensed to the .NET Foundation, MIT
        private static int GetCopyBufferSize(Stream stream)
        {
            // This value was originally picked to be the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
            // The CopyTo{Async} buffer is short-lived and is likely to be collected at Gen0, and it offers a significant improvement in Copy
            // performance.  Since then, the base implementations of CopyTo{Async} have been updated to use ArrayPool, which will end up rounding
            // this size up to the next power of two (131,072), which will by default be on the large object heap.  However, most of the time
            // the buffer should be pooled, the LOH threshold is now configurable and thus may be different than 85K, and there are measurable
            // benefits to using the larger buffer size.  So, for now, this value remains.
            const int defaultCopyBufferSize = 81920;

            int bufferSize = defaultCopyBufferSize;

            if (stream.CanSeek)
            {
                long length = stream.Length;
                long position = stream.Position;
                if (length <= position) // Handles negative overflows
                {
                    // There are no bytes left in the stream to copy.
                    // However, because CopyTo{Async} is virtual, we need to
                    // ensure that any override is still invoked to provide its
                    // own validation, so we use the smallest legal buffer size here.
                    bufferSize = 1;
                }
                else
                {
                    long remaining = length - position;
                    if (remaining > 0)
                    {
                        // In the case of a positive overflow, stick to the default size
                        bufferSize = (int)Math.Min(bufferSize, remaining);
                    }
                }
            }

            return bufferSize;
        }


        private static string GetGenePath(GeneType? type)
        {
            return type switch
            {
                GeneType.Catlet => "",
                GeneType.Volume => "volumes",
                GeneType.Fodder => "fodder",
                null => "",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        private async Task DecompressXz(GeneManifestData geneManifest, Stream stream, string genesetDirectory, CancellationToken cancel)
        {
            InitNativeLibrary();

            var folderName = genesetDirectory;
            var genePath = GetGenePath(geneManifest.Type);
            if (!string.IsNullOrWhiteSpace(genePath))
            {
                folderName = Path.Combine(folderName, genePath);
            }
            
            _fileSystemService.EnsureDirectoryExists(folderName);

            if (geneManifest.FileName == null)
                throw new InvalidOperationException("Missing filename for compressed gene.");

            var fileName = Path.Combine(folderName, geneManifest.FileName);
            await using var fileStream = _fileSystemService.OpenWrite(fileName);
            fileStream.SetLength(geneManifest.OriginalSize.GetValueOrDefault());

            var threadOpts = new XZThreadedDecompressOptions
            {
                Threads = Environment.ProcessorCount > 8
                    ? Environment.ProcessorCount -2
                    : Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 1 : 1, 
                MemlimitThreading = XZHardware.PhysMem() / 4
            };

            await using var xzs = new XZStream(stream, new XZDecompressOptions(), threadOpts);
            await DecompressStream(xzs, fileStream, geneManifest.OriginalSize.GetValueOrDefault(), cancel);
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
