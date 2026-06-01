using System;
using System.IO;

namespace Eryph.Security.Cryptography;

/// <summary>
/// Writes private key / certificate material so it is owner-only from the moment it exists. On Unix
/// the file is created with mode <c>0600</c> atomically (via <see cref="FileStreamOptions.UnixCreateMode"/>),
/// so there is no window where the umask leaves it group/world-readable; directories are created
/// <c>0700</c>. On Windows, NTFS ACL inheritance from the (operator-secured) directory applies.
/// </summary>
public static class SecureFile
{
    public static void CreateOwnerOnlyDirectory(string path)
    {
        if (OperatingSystem.IsWindows() || Directory.Exists(path))
            Directory.CreateDirectory(path);
        else
            Directory.CreateDirectory(
                path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>
    /// Atomically writes <paramref name="contents"/> to <paramref name="path"/> owner-only: content is
    /// written to a uniquely-named temp file (created owner-only) and moved into place, so a crash
    /// cannot leave a readable partial file and concurrent writers cannot clobber each other's temp.
    /// </summary>
    public static void WriteOwnerOnly(string path, byte[] contents)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            using (var stream = new FileStream(tempPath, options))
                stream.Write(contents);

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // best effort — do not mask the original failure
            }
            throw;
        }
    }
}
