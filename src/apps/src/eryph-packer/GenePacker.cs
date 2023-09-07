using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joveler.Compression.XZ;

namespace Eryph.Packer;

public static class GenePacker
{

    public static async Task<string> CreateGene(PackableFile file, string genesetDir, Dictionary<string, string> metadata, CancellationToken token)
    {
        InitNativeLibrary();

        await using var fileStream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var tempName = Guid.NewGuid().ToString();

        var tempDir = Path.Combine(genesetDir, tempName);
        Directory.CreateDirectory(tempDir);
        var compressedPath = Path.Combine(tempDir, "compressed");

        var compOpts = new XZCompressOptions
        {
            Level = LzmaCompLevel.Default,
            ExtremeFlag = true,
            LeaveOpen = true,
        };
        var threadOpts = new XZThreadedCompressOptions
        {
            Threads = Environment.ProcessorCount > 8
                ? Environment.ProcessorCount - 2
                : Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 1 : 1,

        };

        Console.WriteLine($"compressing {Path.GetFileName(file.FullPath)}");
        await using (var targetStream = new FileStream(compressedPath,
                         FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await using var compressionStream = file.ExtremeCompression
                ? (Stream) new XZStream(targetStream, compOpts, threadOpts)
                : new GZipStream(targetStream, CompressionLevel.Fastest, false);

            var bufferSize = GetCopyBufferSize(fileStream);
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int read;
                long totalRead = 0;
                var totalSizeString = ToFormatSize(fileStream.Length);
                var stopWatch = Stopwatch.StartNew();
                while ((read = await fileStream.ReadAsync(new Memory<byte>(buffer), token)) > 0)
                {
                    await compressionStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, read), token);
                    totalRead += read;

                    if (stopWatch.Elapsed.TotalSeconds > 5)
                    {
                        var percent = Math.Round(totalRead *1d / fileStream.Length * 100, 0, MidpointRounding.ToZero);
                        Console.WriteLine($"{percent,3} % - {ToFormatSize(totalRead)} of {totalSizeString} processed ({ToFormatSize(targetStream.Length)} compressed size)");
                        stopWatch.Restart();
                    }
                }
                await compressionStream.FlushAsync(token);

                Console.WriteLine($"100 % - {ToFormatSize(totalRead)} of {totalSizeString} processed ({ToFormatSize(targetStream.Length)} compressed size)");

            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

        }

        return await CreateGene(tempDir, file, metadata, token);
    }

    public static string ToFormatSize(long size)
    {
        return size switch
        {
            < 1024 => $"{size} bytes",
            < 1024 << 10 => $"{Math.Round(size / 1024D, 2)} KB",
            < 1024 << 20 => $"{Math.Round(size * 1D / (1024 << 10), 2):F} MB",
            < 1024L << 30 => $"{Math.Round(size * 1D / (1024L << 20), 2):F} GB",
            < 1024L << 40 => $"{Math.Round(size * 1D / (1024L << 30), 2):F} TB",
            < 1024L << 50 => $"{Math.Round(size * 1D / (1024L << 40), 2):F} PB",
            _ => $"{size} bytes"
        };
    }

    private static async Task<string> CreateGene(string tempDir, PackableFile file, Dictionary<string,string> metadata, CancellationToken token)
    {
        var compressedPath = Path.Combine(tempDir, "compressed");

        var fileInfo = new FileInfo(compressedPath);
        var totalSize = fileInfo.Length;

        var splitFiles = await SplitFile(compressedPath, 1024 * 1024 * 120, token);
        token.ThrowIfCancellationRequested();
        
        File.Delete(compressedPath);

        var parts = new List<string>();
        foreach (var splitFile in splitFiles)
        {
            token.ThrowIfCancellationRequested();

            var hashAlg = SHA1.Create();
            await using (var dataStream = File.OpenRead(splitFile))
            {
                await hashAlg.ComputeHashAsync(dataStream, token);
            }

            var hashString = GetHashString(hashAlg.Hash!);

            var splitFileDir = Path.GetDirectoryName(splitFile) ?? "";
            File.Move(splitFile, Path.Combine(splitFileDir, $"{hashString}.part"));
            parts.Add($"sha1:{hashString}");
        }

        token.ThrowIfCancellationRequested();

        var sourceFile = new FileInfo(file.FullPath);
        var manifestData = new GeneManifest
        {
            
            FileName = file.FileName,
            Name = file.GeneName,
            Size = totalSize,
            OriginalSize = sourceFile.Length,
            Format = file.ExtremeCompression ? "xz" : "gz",
            Parts = parts.ToArray(),
            Metadata = metadata,
            Type = file.GeneType.ToString().ToLowerInvariant()
        };

        var jsonString = JsonSerializer.Serialize(manifestData);

        var sha256 = SHA256.Create();
        var manifestHash = GetHashString(sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonString)));
        await File.WriteAllTextAsync(Path.Combine(tempDir, "gene.json"), jsonString, token);

        var destDir = Path.Combine(Path.GetDirectoryName(tempDir)!, manifestHash);
        if(Directory.Exists(destDir))
            Directory.Delete(destDir, true);

        Directory.Move(tempDir, destDir);

        return $"sha256:{manifestHash}";

    }

    static string GetHashString(byte[] hashBytes)
    {
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static async Task<IEnumerable<string>> SplitFile(string inputFile, int chunkSize, CancellationToken token)
    {
        const int bufferSize = 81920;
        var buffer = (new byte[bufferSize]).AsMemory();

        await using Stream input = File.OpenRead(inputFile);
        var index = 0;
        var fileNames = new List<string>();
        while (input.Position < input.Length)
        {
            if (token.IsCancellationRequested)
                break;

            var fileName = $"{inputFile}.{index}";
            fileNames.Add(fileName);
            await using (Stream output = File.Create(fileName))
            {
                int remaining = chunkSize, bytesRead;
                while (remaining > 0 && (bytesRead = await input.ReadAsync(buffer[..Math.Min(remaining, bufferSize)], token)) > 0)
                {
                    if (token.IsCancellationRequested)
                        break;

                    await output.WriteAsync(buffer[..bytesRead], token);
                    remaining -= bytesRead;
                }
            }
            index++;
        }

        return fileNames;
    }

    private static bool _nativeInitialized;


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


    public static void InitNativeLibrary()
    {
        if(_nativeInitialized)
            return;

        _nativeInitialized = true;

        string libDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ,"runtimes");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            libDir = Path.Combine(libDir, "win-");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            libDir = Path.Combine(libDir, "linux-");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            libDir = Path.Combine(libDir, "osx-");

        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X86:
                libDir += "x86";
                break;
            case Architecture.X64:
                libDir += "x64";
                break;
            case Architecture.Arm:
                libDir += "arm";
                break;
            case Architecture.Arm64:
                libDir += "arm64";
                break;
        }
        libDir = Path.Combine(libDir, "native");

        string libPath = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            libPath = Path.Combine(libDir, "liblzma.dll");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            libPath = Path.Combine(libDir, "liblzma.so");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            libPath = Path.Combine(libDir, "liblzma.dylib");

        if (libPath == null)
            throw new PlatformNotSupportedException($"Unable to find native library.");
        if (!File.Exists(libPath))
            throw new PlatformNotSupportedException($"Unable to find native library [{libPath}].");

        XZInit.GlobalInit(libPath);
    }
}