using System;
using System.IO;

namespace Eryph.ModuleCore.Components;

internal static class SecureFile
{
    /// <summary>
    /// Writes a file owner-only (0600 on Unix) and atomically: the bytes go to a uniquely-named temp
    /// file in the same directory, created owner-only from the start (never momentarily readable by
    /// other users under the umask), then moved into place. A crash mid-write therefore cannot leave a
    /// truncated key / PKCS#12 behind, and a concurrent reader always sees a complete file. The move
    /// preserves the mode (rename keeps the inode on Unix; the owner-only directory ACL governs on
    /// Windows). A partial temp file is removed on failure.
    /// </summary>
    public static void WriteOwnerOnly(string path, byte[] contents)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllBytes(tempPath, contents);
            }
            else
            {
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
                };
                using var stream = new FileStream(tempPath, options);
                stream.Write(contents);
            }

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    /* best effort — a successful Move already removed it */
                }
        }
    }
}
