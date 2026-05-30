using System;
using System.IO;

namespace Eryph.AppCore;

/// <summary>
/// Resolves the configuration root for a standalone runtime. Unlike eryph-zero's fixed
/// <c>ZeroConfig</c> paths, a standalone component reads its root from the
/// <c>ERYPH_CONFIG_PATH</c> environment variable (so multiple components can run on one
/// host without colliding), falling back to the shared ProgramData location.
/// </summary>
public static class AppConfigPaths
{
    public static string GetConfigRoot() =>
        Environment.GetEnvironmentVariable("ERYPH_CONFIG_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph");

    public static string GetControllerSettingsPath() => Ensure(Path.Combine(GetConfigRoot(), "controller"));

    public static string GetNetworksConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "networks"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
