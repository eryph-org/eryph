using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core.Sys;

public interface FileSystemIO
{
    ValueTask<bool> IsInUse(string path);
}

public readonly struct LiveFileSystemIO : FileSystemIO
{
    private const int ErrorSharingViolation = -2147024864;
    private const int EAgain = 11;
    public static readonly FileSystemIO Default = new LiveFileSystemIO();

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
            // TODO Use ElmarIO Hresult
            if (OperatingSystem.IsWindows() && ioe.HResult == ErrorSharingViolation)
                return true;

            if (OperatingSystem.IsLinux() && ioe.HResult == EAgain)
                return true;

            throw;

            
        }
    }
}
