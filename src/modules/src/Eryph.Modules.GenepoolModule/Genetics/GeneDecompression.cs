using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement;
using Joveler.Compression.XZ;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.GenePool.Genetics;

internal class GeneDecompression(
    GeneInfo geneInfo,
    IFileSystemService fileSystemService,
    ILogger log,
    Func<string, int, Task<Unit>> reportProgress)
{
    public async Task Decompress(
        Stream stream,
        string genePoolPath,
        CancellationToken cancel)
    {
        var genePath = GenePoolPaths.GetGenePath(
            genePoolPath,
            geneInfo.Id);

        var geneFolder = Path.GetDirectoryName(genePath);
        fileSystemService.EnsureDirectoryExists(geneFolder);
        var totalSize = geneInfo.MetaData!.OriginalSize.GetValueOrDefault();
        await using var targetStream = fileSystemService.OpenWrite(genePath);

        switch (geneInfo.MetaData!.Format)
        {
            case "plain":
                await CopyPlain(stream, targetStream, totalSize, cancel);
                break;
            case "gz":
                await DecompressGZip(stream, targetStream, totalSize, cancel);
                break;
            case "xz":
                await DecompressXz(stream, targetStream, totalSize, cancel);
                break;
            default: throw new NotSupportedException($"Unsupported gene format {geneInfo.MetaData!.Format}");
        }
    }

    private async Task DecompressGZip(
        Stream source,
        Stream target,
        long totalSize,
        CancellationToken cancellationToken)
    {
        await using var gZipStream = new GZipStream(source, CompressionMode.Decompress);
        await DecompressStream(gZipStream, target, totalSize, cancellationToken);
    }

    private async Task DecompressStream(Stream source, Stream destination, long totalSize, CancellationToken cancellationToken)
    {
        var bufferSize = GetCopyBufferSize(source);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int bytesRead;
            long totalRead = 0;
            var totalMb = Math.Round(totalSize / 1024d / 1024d, 0);
            var stopWatch = Stopwatch.StartNew();

            while ((bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (stopWatch.Elapsed.TotalSeconds <= 10 || totalSize == 0)
                    continue;

                var percent = Math.Round(totalRead / (double)totalSize, 3);
                var totalReadMb = Math.Round(totalRead / 1024d / 1024d, 0);
                var percentInt = Convert.ToInt32(percent * 100d);

                var progressMessage =
                    $"Extracting {geneInfo.Id} ({totalReadMb:F} MiB / {totalMb:F} MiB) => {percent:P1} completed";
                log.LogTrace("Extracting {Gene} ({TotalReadMiB} MiB / {TotalMiB} MiB) => {Percent:P1} completed",
                    geneInfo.Id, totalReadMb, totalMb, percent);
                await reportProgress(progressMessage, percentInt);
                stopWatch.Restart();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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

    private static async Task CopyPlain(
        Stream source,
        Stream target,
        long totalSize,
        CancellationToken cancellationToken)
    {
        target.SetLength(totalSize);
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task DecompressXz(
        Stream source,
        Stream target,
        long totalSize,
        CancellationToken cancellationToken)
    {
        InitNativeLibrary();

        target.SetLength(totalSize);

        var threadOpts = new XZThreadedDecompressOptions
        {
            Threads = Environment.ProcessorCount switch
            {
                >= 8 => Environment.ProcessorCount - 2,
                > 2 => Environment.ProcessorCount - 1,
                _ => 1,
            },
            MemlimitThreading = XZHardware.PhysMem() / 4
        };

        await using var xzStream = new XZStream(source, new XZDecompressOptions(), threadOpts);
        await DecompressStream(xzStream, target, totalSize, cancellationToken);
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
