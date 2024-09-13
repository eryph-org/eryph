using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

internal class OVSPackage
{
    public static string UnpackAndProvide(ILogger<OVSPackage> logger, string? relativePackagePath = null)
    {
        var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        var parentDir = baseDir.Parent?.FullName ?? throw new IOException($"Invalid path {baseDir}");

        if (relativePackagePath != null)
        {
           parentDir =  Path.Combine(parentDir, relativePackagePath);
           if (!Directory.Exists(parentDir))
           {
               throw new IOException($"Invalid path '{parentDir}'");
           }
        }

        var ovsPackageFile = Path.Combine(parentDir, "ovspackage.zip");
        var ovsPackageExists = File.Exists(ovsPackageFile);
        if (!File.Exists(ovsPackageFile))
            throw new IOException("The OVS package is missing.");

        var ovsRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "ovs");

        logger.LogDebug("OVS Package installation: root: {RootPath}, package file: {OvsPackageExists}",
            parentDir, ovsPackageExists);

        var ovsRootDir = new DirectoryInfo(ovsRootPath);
        ovsRootDir.Create();
        ovsRootDir.SetAccessControl(GetOvsDirectorySecurity());

        var ovsRunDirectory = FindRunDirectory(ovsRootDir);
        if (ovsRunDirectory is not null)
        {
            logger.LogInformation("Found existing OpenVSwitch installation '{OvsDirectory}'", ovsRunDirectory.FullName);
            var stopWatch = Stopwatch.StartNew();
            var isValid = IsRunDirValid(ovsRunDirectory, ovsPackageFile);
            logger.LogInformation("Run dir check took: {MilliSeconds}ms", stopWatch.ElapsedMilliseconds);
            if (!isValid)
            {
                logger.LogInformation("Existing OpenVSwitch installation is outdated or incomplete. Reinstalling...");
                ovsRunDirectory = null;
            }
        }

        if (ovsRunDirectory is null)
        {
            logger.LogInformation("Installing OpenVSwitch package...");
            var nextRunDirNo = ovsRootDir.GetDirectories("run_*")
                .Map(d => Prelude.parseInt(d.Name.Replace("run_", "")))
                .Somes()
                .Fold(0, Math.Max) + 1;
            ovsRunDirectory = new DirectoryInfo(Path.Combine(ovsRootDir.FullName, $"run_{nextRunDirNo:D}"));
            ZipFile.ExtractToDirectory(ovsPackageFile, ovsRunDirectory.FullName);
            logger.LogInformation("OpenVSwitch package successfully installed.");
        }

        // Cleanup old OVS installations (if not in use anymore)
        foreach (var directory in ovsRootDir.GetDirectories("run_*"))
        {
            if (directory.FullName == ovsRunDirectory.FullName)
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

        return ovsRunDirectory.FullName;
    }

    public static string? GetCurrentOVSPath()
    {
        var ovsRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "ovs");
        
        var ovsRootDir = new DirectoryInfo(ovsRootPath);
        if (!ovsRootDir.Exists)
            return null;

        var ovsRunDir = FindRunDirectory(ovsRootDir);
        return ovsRunDir?.FullName;
    }

    private static DirectoryInfo? FindRunDirectory(DirectoryInfo rootDirectory)
    {
        DirectoryInfo? bestMatch = null;
        var bestMatchNo = 0;
        foreach (var ovsFilesDir in rootDirectory.GetDirectories("run_*"))
        {
            if (ovsFilesDir.GetFiles("ovs-vsctl.exe", SearchOption.AllDirectories).Length == 0)
                continue;

            var name = ovsFilesDir.Name.Replace("run_", "");
            if (int.TryParse(name, out var currentNo) && currentNo > bestMatchNo)
            {
                bestMatch = ovsFilesDir;
                bestMatchNo = currentNo;
            }
            else if (bestMatch is null)
            {
                bestMatch = ovsFilesDir;
            }
        }

        return bestMatch;
    }

    private static DirectorySecurity GetOvsDirectorySecurity()
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

    public static bool IsRunDirValid(DirectoryInfo ovsRunDirectory, string packagePath)
    {
        var runDirFiles = ovsRunDirectory.GetFiles("*", SearchOption.AllDirectories);
        var crc32 = new Crc32();

        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
        {
            var entryPath = Path.GetFullPath(entry.FullName, ovsRunDirectory.FullName);
            var fileInfo = runDirFiles.FirstOrDefault(
                fi => string.Equals(entryPath, fi.FullName, StringComparison.OrdinalIgnoreCase));

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
