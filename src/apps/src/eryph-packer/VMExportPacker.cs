using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joveler.Compression.XZ;

namespace Eryph.Packer;

public class VMExportPacker
{
    public async Task<IEnumerable<string>> PackToArtifacts(DirectoryInfo vmExport, string artifactsFolder, CancellationToken token)
    {
        InitNativeLibrary();
        
        var vhdFiles = vmExport.GetFiles("*.vhdx", SearchOption.AllDirectories);

        var processedFiles = new List<string>();
        var artifacts = new List<string>();
        foreach (var vhdFile in vhdFiles)
        {
            token.ThrowIfCancellationRequested();

            artifacts.Add(await CreateFileArtifact(vhdFile.FullName, vmExport.FullName, artifactsFolder, token));
            var relativePath = Path.GetRelativePath(vmExport.FullName, vhdFile.FullName);
            processedFiles.Add(relativePath);

        }

        artifacts.Add(await CreateDirectoryArtifact(vmExport.FullName, artifactsFolder, processedFiles.ToArray(), token));

        return artifacts;

    }

    static async Task<string> CreateDirectoryArtifact(string directory, string outputDir, string[] ignoredFiles, CancellationToken token)
    {
        var tempName = Guid.NewGuid().ToString();
        var tempDir = Path.Combine(outputDir, tempName);
        Directory.CreateDirectory(tempDir);
        var compressedPath = Path.Combine(tempDir, "compressed");

        await using var zipStream = File.Create(compressedPath);
        var dirInfo = new DirectoryInfo(directory);

        using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, false))
        {
            foreach (var fileInfo in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(dirInfo.FullName, fileInfo.FullName);
                if (ignoredFiles.Contains(relativePath))
                    continue;

                var zipEntry = zipArchive.CreateEntry(relativePath, CompressionLevel.Optimal);
                await using var zipEntryStream = zipEntry.Open();
                await using var fileStream = fileInfo.OpenRead();
                await fileStream.CopyToAsync(zipEntryStream);
            }
        }

        return await CreateArtifactFromTempDir(tempDir, null, false, token);
    }

    static async Task<string> CreateFileArtifact(string filePath, string sourceDir, string outputDir, CancellationToken token)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var relativePath = Path.GetRelativePath(sourceDir, filePath);

        var tempName = Guid.NewGuid().ToString();
        var tempDir = Path.Combine(outputDir, tempName);
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
                : Environment.ProcessorCount > 2 ? Environment.ProcessorCount - 1 : 1
        };

        Console.WriteLine($"compressing {relativePath}");
        await using (var targetStream = new FileStream(compressedPath,
                         FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await using var xzs = new XZStream(targetStream, compOpts, threadOpts);
            await fileStream.CopyToAsync(xzs, token);
            await xzs.FlushAsync(token);
        }

        return await CreateArtifactFromTempDir(tempDir, relativePath, true, token);
    }

    static async Task<string> CreateArtifactFromTempDir(string tempDir, string? relativePath, bool singleFile, CancellationToken token)
    {
        var compressedPath = Path.Combine(tempDir, "compressed");

        var fileInfo = new FileInfo(compressedPath);
        var totalSize = fileInfo.Length;

        var splitFiles = await SplitFile(compressedPath, 1024 * 1024 * 64, token);
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

            var splitFileDir = Path.GetDirectoryName(splitFile);
            File.Move(splitFile, Path.Combine(splitFileDir, hashString));
            parts.Add($"sha1:{hashString}");
        }

        token.ThrowIfCancellationRequested();

        var manifestData = new ArtifactManifest
        {
            Path = singleFile ? Path.GetDirectoryName(relativePath) : relativePath,
            FileName = singleFile ? Path.GetFileName(relativePath) : null,
            Size = totalSize,
            Format = singleFile ? "xz" : "zip",
            Parts = parts.ToArray()
        };

        var jsonString = JsonSerializer.Serialize(manifestData);

        var sha256 = SHA256.Create();
        var manifestHash = GetHashString(sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonString)));
        await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), jsonString, token);

        Directory.Move(tempDir, Path.Combine(Path.GetDirectoryName(tempDir)!, manifestHash));

        return $"sha256:{manifestHash}";

    }

    static string GetHashString(byte[] hashBytes)
    {
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static async Task<IEnumerable<string>> SplitFile(string inputFile, int chunkSize, CancellationToken token)
    {
        const int bufferSize = 20 * 1024;
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

    private static bool NativeInitialized;

    static void InitNativeLibrary()
    {
        if(NativeInitialized)
            return;

        NativeInitialized = true;

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