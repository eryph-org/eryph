using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Eryph.Security.Cryptography;

/// <summary>
/// Writes private key / certificate material so it is owner-only from the moment it exists. On Unix
/// the file is created with mode <c>0600</c> atomically (via <see cref="FileStreamOptions.UnixCreateMode"/>),
/// so there is no window where the umask leaves it group/world-readable; directories are created
/// <c>0700</c>. On Windows a newly created directory gets an inheritance-protected ACL granting only the
/// current user, local Administrators and SYSTEM (the default inherited ACL under e.g. %ProgramData%
/// grants Users read), and files written into it inherit that ACL.
/// </summary>
public static class SecureFile
{
    public static void CreateOwnerOnlyDirectory(string path)
    {
        if (Directory.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
            CreateRestrictedWindowsDirectory(path);
        else
            Directory.CreateDirectory(
                path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void CreateRestrictedWindowsDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User
                    ?? throw new InvalidOperationException(
                        "Cannot determine the current user to restrict the directory.");

        var security = new DirectorySecurity();
        // Protect from inheritance so the (potentially world-readable) parent ACL does not apply, and
        // grant full control only to owner + Administrators + SYSTEM, inherited by child files/dirs.
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[]
                 {
                     owner,
                     new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                     new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                 })
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

        security.CreateDirectory(path);
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
            {
                stream.Write(contents);
            }

            File.Move(tempPath, path, true);
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
