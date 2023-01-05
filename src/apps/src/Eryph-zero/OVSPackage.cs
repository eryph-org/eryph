using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Security;
using Serilog;
using Serilog.Core;

namespace Eryph.Runtime.Zero;

internal class OVSPackage
{
    public static string UnpackAndProvide(bool canInstallDriver, string? relativePackagePath = null)
    {
        var log = Log.ForContext<OVSPackage>();
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
        var ovsPackageBackupFile = Path.Combine(parentDir, "ovspackage_backup.zip");

        
        var ovsPackageExists = File.Exists(ovsPackageFile);
        var ovsBackupExists = File.Exists(ovsPackageBackupFile);

        var ovsRootPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "eryph", "ovs");

        log.Debug("OVS Package installation: Package root: {rootPath}, package file found: {ovsPackageExists}, backup file found: {ovsBackupExists}",
            parentDir, ovsPackageExists, ovsBackupExists);

        var ovsRootDir = new DirectoryInfo(ovsRootPath);
        if(!ovsRootDir.Exists)
            ovsRootDir.Create(GetOvsDirectorySecurity());

        var runDirNo = 0;
        foreach (var ovsFilesDir in ovsRootDir.GetDirectories("run_*"))
        {
            log.Verbose("Checking path '{ovsFileDir}' for ovs files", ovsFilesDir.FullName);
            var hasOvs = ovsFilesDir.GetFiles("ovs-vswitchd.exe", SearchOption.AllDirectories).Any();
           
            if(!hasOvs) continue;
           
            var name = ovsFilesDir.Name.Replace("run_", "");
            if(!int.TryParse(name, out var dirNo)) continue;

            if (dirNo > runDirNo)
            {
                log.Verbose("Found ovs folder, run dir no increased to {runDirNo} ", runDirNo);
                runDirNo = dirNo;
            }
        }

        if (runDirNo == 0)
        {

            if (!ovsPackageExists && !ovsBackupExists)
                throw new IOException(
                    "Could not find existing OpenVSwitch run directory and ovs package to install ovs.");

            if (ovsBackupExists && relativePackagePath== null)
            {
                log.Information("No OVS Installation folder found. Trying to recreate from backup.");
                File.Move(ovsPackageBackupFile, ovsPackageFile);
                ovsPackageExists = true;
            }
        }

        if (ovsPackageExists)
        {
            log.Information("Installing Open VSwitch package");

            runDirNo++;
            var extractFolder = Path.Combine(ovsRootDir.FullName, $"run_{runDirNo:D}");
            ZipFile.ExtractToDirectory(ovsPackageFile, extractFolder);


            if (canInstallDriver)
            {
                var exitCode = -1;
                try
                {
                    log.Information("Installing Open VSwitch driver");


                    var driverPathDir = new DirectoryInfo(Path.Combine(extractFolder, "driver"));

                    if(Directory.Exists(Path.Combine(ovsRootPath, "driver")))
                        Directory.Delete(Path.Combine(ovsRootPath, "driver"), true);

                    driverPathDir.MoveTo(Path.Combine(ovsRootPath, "driver"));

#if DEBUG
                    //for development allow installation of self signed driver package
                    if (File.Exists(Path.Combine(driverPathDir.FullName, "package.cer")))
                    {
                        var certService = new WindowsCertificateStoreService();
                        var cert = X509Certificate.CreateFromCertFile(Path.Combine(driverPathDir.FullName,
                            "package.cer"));
                        certService.AddAsRootCertificate(DotNetUtilities.FromX509Certificate(cert));
                    }
#endif
                        var process = Process.Start(new ProcessStartInfo("C:\\Windows\\system32\\netcfg.exe",
                        $"-l \"dbo_ovse.inf\" -c s -i DBO_OVSE")
                    {
                        WorkingDirectory = driverPathDir.FullName,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    });

                    if (process != null)
                    {
                        process.WaitForExit();
                        exitCode = process.HasExited ? process.ExitCode : exitCode;

                        if (exitCode != 0)
                        {
                            var message = process.StandardError.ReadToEnd();
                            if(string.IsNullOrWhiteSpace(message))
                                message = process.StandardOutput.ReadToEnd();

                            log.Error("Driver installation failed with: {message}", message);
                        }
                    }

                }
                catch
                {
                    //ignored
                }

                if (exitCode != 0)
                    throw new IOException(
                        $"Failed to install eryph overlay driver. Installation exit code: {exitCode}");

                if(relativePackagePath==null)
                    File.Move(ovsPackageFile, ovsPackageBackupFile, true);

            }
        }

        //cleanup old rundirs (if not in use any more)
        foreach (var ovsFilesDir in ovsRootDir.GetDirectories("run_*"))
        {
            var name = ovsFilesDir.Name.Replace("run_", "");
            if (!int.TryParse(name, out var dirNo)) continue;

            if(dirNo == runDirNo) continue;

            try
            {
                ovsFilesDir.Delete(true);

            }
            catch
            {
                // folder in use - ignore
            }
        }

        var resolvedRunDir = new DirectoryInfo(Path.Combine(ovsRootPath, $"run_{runDirNo:D}"));

        // should not happen, but to be sure
        if(!resolvedRunDir.GetFiles("ovs-vswitchd.exe", SearchOption.AllDirectories).Any())
            throw new IOException($"Could not find OpenVSwitch in directory {resolvedRunDir}");

        log.Debug("Found OVS package in {ovsPackage}", resolvedRunDir.FullName);

        return resolvedRunDir.FullName;

    }


    public static DirectorySecurity GetOvsDirectorySecurity()
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
}