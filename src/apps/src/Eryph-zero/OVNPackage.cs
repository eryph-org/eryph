using System;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

internal class OVNPackage
{
    public static string UnpackAndProvide(ILogger<OVNPackage> logger, string? relativePackagePath = null)
    {
        var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        var parentDir = baseDir.Parent?.FullName ?? throw new IOException("Invalid OVN package path");

        if (relativePackagePath is not null) parentDir = Path.Combine(parentDir, relativePackagePath);

        var ovnPackageFile = Path.Combine(parentDir, "ovnpackage.zip");
        if (!File.Exists(ovnPackageFile))
            throw new IOException($"The OVN package is missing: '{ovnPackageFile}'");

        var ovnRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "ovn");

        logger.LogDebug("Using OVN package '{OvnPackageFile}'.", ovnPackageFile);

        var ovnRootDir = new DirectoryInfo(ovnRootPath);
        ovnRootDir.Create();
        ovnRootDir.SetAccessControl(GetOvnDirectorySecurity());

        var ovnRunDirectory = FindRunDirectory(ovnRootDir);
        if (ovnRunDirectory is not null)
        {
            logger.LogInformation("Found existing OVN installation '{OvnDirectory}'", ovnRunDirectory.FullName);
            var isValid = IsRunDirValid(ovnRunDirectory, ovnPackageFile);
            if (!isValid)
            {
                logger.LogInformation("Existing OVN installation is outdated or incomplete. Reinstalling...");
                ovnRunDirectory = null;
            }
        }

        if (ovnRunDirectory is null)
        {
            logger.LogInformation("Installing OVN package...");
            var nextRunDirNo = ovnRootDir.GetDirectories("run_*")
                .Map(d => Prelude.parseInt(d.Name.Replace("run_", "")))
                .Somes()
                .Fold(0, Math.Max) + 1;
            ovnRunDirectory = new DirectoryInfo(Path.Combine(ovnRootDir.FullName, $"run_{nextRunDirNo:D}"));
            ZipFile.ExtractToDirectory(ovnPackageFile, ovnRunDirectory.FullName);
            logger.LogInformation("OVN package successfully installed.");
        }

        // Cleanup old OVN installations (if not in use anymore)
        foreach (var directory in ovnRootDir.GetDirectories("run_*"))
        {
            if (directory.FullName == ovnRunDirectory.FullName)
                continue;

            try
            {
                directory.Delete(true);
            }
            catch
            {
                // folder in use - ignore
            }
        }

        return ovnRunDirectory.FullName;
    }

    public static string? GetCurrentOvnPath()
    {
        var ovnRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "ovn");

        var ovnRootDir = new DirectoryInfo(ovnRootPath);
        if (!ovnRootDir.Exists)
            return null;

        var ovnRunDir = FindRunDirectory(ovnRootDir);
        return ovnRunDir?.FullName;
    }

    public static string GetOvnDataPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "openvswitch");

    private static DirectoryInfo? FindRunDirectory(DirectoryInfo rootDirectory)
    {
        DirectoryInfo? bestMatch = null;
        var bestMatchNo = 0;
        foreach (var ovnFilesDir in rootDirectory.GetDirectories("run_*"))
        {
            if (ovnFilesDir.GetFiles("ovs-vsctl.exe", SearchOption.AllDirectories).Length == 0)
                continue;

            var name = ovnFilesDir.Name.Replace("run_", "");
            if (int.TryParse(name, out var currentNo) && currentNo > bestMatchNo)
            {
                bestMatch = ovnFilesDir;
                bestMatchNo = currentNo;
            }
            else if (bestMatch is null)
            {
                bestMatch = ovnFilesDir;
            }
        }

        return bestMatch;
    }

    private static DirectorySecurity GetOvnDirectorySecurity()
    {
        var directorySecurity = new DirectorySecurity();
        IdentityReference adminId = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var adminAccess = new FileSystemAccessRule(
            adminId,
            FileSystemRights.FullControl,
            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

        IdentityReference systemId = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var systemAccess = new FileSystemAccessRule(
            systemId,
            FileSystemRights.FullControl,
            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

        IdentityReference usersId = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var usersAccess = new FileSystemAccessRule(
            usersId,
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

        directorySecurity.AddAccessRule(adminAccess);
        directorySecurity.AddAccessRule(systemAccess);
        directorySecurity.AddAccessRule(usersAccess);
        // set the owner and the group to admins
        directorySecurity.SetAccessRuleProtection(true, true);

        return directorySecurity;
    }

    public static bool IsRunDirValid(DirectoryInfo ovnRunDirectory, string packagePath)
    {
        var runDirFiles = ovnRunDirectory.GetFiles("*", SearchOption.AllDirectories);
        var crc32 = new Crc32();

        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith('/')))
        {
            var entryPath = Path.GetFullPath(entry.FullName, ovnRunDirectory.FullName);
            var fileInfo = runDirFiles.FirstOrDefault(fi =>
                string.Equals(entryPath, fi.FullName, StringComparison.OrdinalIgnoreCase));

            if (fileInfo is null)
                return false;

            crc32.Reset();

            using var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            crc32.Append(stream);

            // Use CRC32 to detect changed files as we can get the CRC32 checksums from the ZIP
            // archive without decompression.
            if (entry.Crc32 != crc32.GetCurrentHashAsUInt32())
                return false;
        }

        return true;
    }
}
