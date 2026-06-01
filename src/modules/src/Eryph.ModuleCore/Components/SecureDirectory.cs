using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Eryph.ModuleCore.Components;

internal static class SecureDirectory
{
    /// <summary>
    /// Creates the directory restricted to its owner on first creation, so private key material in it is
    /// not reachable by other local users even if individual file modes are permissive: 0700 on Unix,
    /// and on Windows an inheritance-protected ACL granting only the current user, local Administrators
    /// and SYSTEM (the default inherited ACL under e.g. %ProgramData% grants Users read). An existing
    /// directory's permissions are left to the deployment tooling.
    /// </summary>
    public static void EnsureOwnerOnly(string path)
    {
        if (Directory.Exists(path))
            return;

        if (OperatingSystem.IsWindows())
            CreateRestrictedWindows(path);
        else
            Directory.CreateDirectory(
                path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void CreateRestrictedWindows(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User
            ?? throw new InvalidOperationException(
                "Cannot determine the current user to restrict the certificate directory.");

        var security = new DirectorySecurity();
        // Protect from inheritance so the (potentially world-readable) parent ACL does not apply, and
        // grant full control only to owner + Administrators + SYSTEM, inherited by child files/dirs so
        // the key/PFX files written into it are restricted too.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[]
                 {
                     owner,
                     new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                     new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                 })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        security.CreateDirectory(path);
    }
}
