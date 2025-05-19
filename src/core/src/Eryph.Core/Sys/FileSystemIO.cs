using LanguageExt;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Eryph.Core.Sys;

public interface FileSystemIO
{
    ValueTask<uint> GetCrc32(string path);

    ValueTask<bool> IsInUse(string path);

    Unit SetAccessControl(string path, DirectorySecurity directorySecurity);

    ValueTask<Unit> ExtractToDirectory(string archivePath, string destinationPath);
}

public readonly struct LiveFileSystemIO : FileSystemIO
{
    private const int ErrorSharingViolation = unchecked((int)0x80070020);
    private const int ErrorLockViolation = unchecked((int)0x80070021);
    private const int EAgain = 11;
    public static readonly FileSystemIO Default = new LiveFileSystemIO();

    public async ValueTask<Unit> ExtractToDirectory(
        string archivePath,
        string destinationPath)
    {
        // TODO mark as long-running task?
        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(archivePath, destinationPath);
        }).ConfigureAwait(false);

        return unit;
    }

    public async ValueTask<bool> IsInUse(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException ioe)
        {
            // Unfortunately, file system errors can have different HResult values
            // depending on the OS. See https://github.com/dotnet/runtime/issues/25998.
            if (OperatingSystem.IsWindows() && ioe.HResult is ErrorSharingViolation or ErrorLockViolation)
                return true;

            if (OperatingSystem.IsLinux() && ioe.HResult is EAgain)
                return true;

            throw;
        }
    }

    public Unit SetAccessControl(
        string path,
        DirectorySecurity directorySecurity)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException();

        var directoryInfo = new DirectoryInfo(path);
        directoryInfo.SetAccessControl(directorySecurity); 
        
        return unit;
    }

    public async ValueTask<uint> GetCrc32(string path)
    {
        var crc32 = new Crc32();
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await crc32.AppendAsync(stream);
        return crc32.GetCurrentHashAsUInt32();
    }
}
