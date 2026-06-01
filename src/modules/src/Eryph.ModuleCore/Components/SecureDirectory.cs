using System;
using System.IO;

namespace Eryph.ModuleCore.Components;

internal static class SecureDirectory
{
    /// <summary>
    /// Creates the directory owner-only (0700 on Unix) on first creation, so private key material in
    /// it is not reachable by other users even if individual file modes are permissive. An existing
    /// directory's permissions are left to the deployment tooling.
    /// </summary>
    public static void EnsureOwnerOnly(string path)
    {
        if (!OperatingSystem.IsWindows() && !Directory.Exists(path))
            Directory.CreateDirectory(
                path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else
            Directory.CreateDirectory(path);
    }
}
