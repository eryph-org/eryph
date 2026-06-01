using System;
using System.IO;

namespace Eryph.ModuleCore.Components;

internal static class SecureFile
{
    /// <summary>
    /// Writes a file owner-only (0600 on Unix) from the moment it is created, so private-key material
    /// is never momentarily readable by other users under the process umask. On Windows the access is
    /// governed by the (owner-only) directory ACL, so a plain write is used.
    /// </summary>
    public static void WriteOwnerOnly(string path, byte[] contents)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(path, contents);
            return;
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        using var stream = new FileStream(path, options);
        stream.Write(contents);
    }
}
