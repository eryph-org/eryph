using System;
using System.IO;
using Eryph.Core.Sys;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using System.Security.AccessControl;
using System.Security.Principal;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.PowerShell.Commands;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public class OvsPackageProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasDirectory<RT>,
    HasDism<RT>,
    HasFile<RT>,
    HasFileSystem<RT>,
    HasHostNetworkCommands<RT>,
    HasLogger<RT>,
    HasPowershell<RT>,
    HasProcessRunner<RT>,
    HasRegistry<RT>,
    HasZip<RT>
{
    public static Aff<RT, string> ensureOvsDirectory(string ovsPackagePath) =>
        from _1 in unitAff
        let ovsPackageFile = Path.IsPathFullyQualified(ovsPackagePath)
            ? ovsPackagePath
            : Path.Combine(AppContext.BaseDirectory, ovsPackagePath)
        from ovsPackageFileExists in File<RT>.exists(ovsPackageFile)
        from _2 in guard(ovsPackageFileExists, Error.New($"The OpenVSwitch package is missing: '{ovsPackageFile}'"))
        from _3 in logDebug("Using OpenVSwitch package '{OvsPackageFile}'.", ovsPackageFile)
        // TODO add logging
        from ovsRootPath in ensureOvsRootDirectory()
        from existingRunDir in findRunDirectory(ovsRootPath)
        // TODO add version check
        from isExistingRunDirValid in existingRunDir
            .Map(p => isRunDirValid(p, ovsPackageFile))
            .Sequence()
            .Map(o => o.IfNone(false))
        from runDirPath in existingRunDir
            .Filter(_ => isExistingRunDirValid)
            .Match(Some: p => SuccessAff(p), None: () => extractOvsPackage(ovsRootPath, ovsPackageFile))
        //TODO add cleanup
        select runDirPath;

    private static Aff<RT, string> extractOvsPackage(
        string ovsRootPath,
        string ovsPackagePath) =>
        from _1 in logInformation("Installing OpenVSwitch package...")
        from nextRunDirPath in findNextRunDirectory(ovsRootPath)
        from _2 in Zip<RT>.extractToDirectory(ovsPackagePath, nextRunDirPath)
        from _3 in logInformation("OpenVSwitch package successfully installed.")
        select nextRunDirPath;

    private static Eff<RT, string> ensureOvsRootDirectory() =>
        from _1 in unitEff
        let ovsRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "ovs")
        from directoryExists in Directory<RT>.exists(ovsRootPath)
        from _2 in directoryExists
            ? unitEff
            : Directory<RT>.create(ovsRootPath)
        from _3 in FileSystem<RT>.setAccessControl(ovsRootPath, GetOvsDirectorySecurity())
        select ovsRootPath;

    private static Eff<RT, Option<string>> findRunDirectory(string ovsRootPath) =>
        from candidates in Directory<RT>.enumerateDirectories(ovsRootPath, "run_*")
        from validCandidates in candidates
            .Map(Foo)
            .Sequence()
        let bestCandidate = validCandidates
            .Somes()
            .OrderByDescending(x => x.Number)
            .HeadOrNone()
        select bestCandidate.Map(t => t.Path);

    private static Eff<RT, string> findNextRunDirectory(string ovsRootDir) =>
        from existingDirectories in Directory<RT>.enumerateDirectories(ovsRootDir, "run_*")
        let nextNumber = existingDirectories
            .Map(d => parseInt(Path.GetFileName(d).Replace("run_", "")))
            .Somes()
            .Fold(0, Math.Max) + 1
        select Path.Combine(ovsRootDir, $"run_{nextNumber:D}");

    private static Eff<RT, Option<(string Path, int Number)>> Foo(string path) =>
        from hasInfFile in File<RT>.exists(Path.Combine(path, "driver", "DBO_OVSE.inf"))
        let number = parseInt(Path.GetFileName(path).Replace("run_", ""))
        select from validNumber in number
               where hasInfFile
               select (path, validNumber);


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

    private static Aff<RT, bool> isRunDirValid(string runDirectoryPath, string packagePath) =>
        ensureRunDirValid(runDirectoryPath, packagePath)
            .Match(Succ: true, Fail: false);

    private static Aff<RT, Unit> ensureRunDirValid(string runDirectoryPath, string packagePath) =>
        from entries in Zip<RT>.getEntries(packagePath)
        from _ in entries
            .Map(entry => ensureEntryValid(entry, runDirectoryPath))
            .SequenceSerial()
        select unit;

    private static Aff<RT, Unit> ensureEntryValid(
        ZipArchiveEntryMetadata entry,
        string ovsRunDirectoryPath) =>
        from _ in unitAff
        let entryPath = Path.GetFullPath(entry.FullName, ovsRunDirectoryPath)
        from fileExists in File<RT>.exists(entryPath)
        from _2 in guard(fileExists, Error.New($"The entry '{entry.FullName}' is missing."))
        from fileCrc32 in FileSystem<RT>.getCrc32(entryPath)
        // Use CRC32 to detect changed files as we can get the CRC32 checksums from the ZIP
        // archive without decompression.
        from _3 in guard(entry.Crc32 == fileCrc32,
            Error.New($"The entry '{entry.FullName}' has different content (CRC32 mismatch)."))
        select unit;


    private static Eff<RT, Unit> logInformation(string? message, params object?[] args) =>
        Logger<RT>.logInformation<OvsPackageProvider<RT>>(message, args);

    private static Eff<RT, Unit> logDebug(string? message, params object?[] args) =>
        Logger<RT>.logDebug<OvsPackageProvider<RT>>(message, args);

    // TODO use driver inf file to determine the package version
}
