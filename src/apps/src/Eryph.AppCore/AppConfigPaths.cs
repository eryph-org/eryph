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

    public static string GetVmHostAgentConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "agentsettings"));

    public static string GetGenePoolSettingsPath() => Ensure(Path.Combine(GetConfigRoot(), "genepool"));

    public static string GetProjectsConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "projects"));

    public static string GetProjectNetworksConfigPath() => Ensure(Path.Combine(GetProjectsConfigPath(), "networks"));

    public static string GetProjectNetworkPortsConfigPath() => Ensure(Path.Combine(GetProjectsConfigPath(), "ports"));

    public static string GetMetadataConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "vms", "md"));

    public static string GetCatletSpecificationsConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "catlets", "specs"));

    public static string GetCatletSpecificationVersionsConfigPath() => Ensure(Path.Combine(GetConfigRoot(), "catlets", "specversions"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
