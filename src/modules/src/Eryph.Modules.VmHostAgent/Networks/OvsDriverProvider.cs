using Eryph.Modules.VmHostAgent.Networks.Powershell;
using LanguageExt.Effects.Traits;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public class OvsDriverProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasLogger<RT>,
    HasPowershell<RT>,
    HasDirectory<RT>,
    HasFile<RT>,
    HasProcessRunner<RT>,
    HasEnvironment<RT>,
    HasHostNetworkCommands<RT>
{
    public static Aff<RT, Unit> ensureDriver(string ovsRunDir, bool canInstall, bool canUpgrade) =>
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
        from _ in match(extensionInfo,
            Some: ei => logInformation("OVS Hyper-V switch extension {ExtensionVersion} is installed", ei.Version),
            None: () => logInformation("OVS Hyper-V switch extension is not installed"))
        let infPath = Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf")
        from infVersion in getDriverVersionFromInfFile(infPath)
        from __ in match(extensionInfo,
            Some: ei =>
                from extensionVersion in parseVersion(ei.Version).ToAff(Error.New(
                    "Could not parse the version of the Hyper-V extension"))
                from _ in extensionVersion != infVersion && canUpgrade
                    ? from _ in removeAllDriverPackages()
                      from __ in installDriver(infPath)
                      select unit
                    : from _ in extensionVersion != infVersion
                        ? logWarning("Hyper-V switch extension version {ExtensionVersion} does not match packaged driver version {DriverVersion}",
                            ei.Version, infVersion)
                        : SuccessAff<RT, Unit>(unit)
                      select unit
                select unit,
            None: () => canInstall
                ? installDriver(infPath)
                : FailAff<RT, Unit>(Error.New("OVS Hyper-V switch extension is missing")))
        select unit;

    public static Aff<RT, Unit> installDriver(string infPath) =>
        from _ in logInformation("Going to install OVS Hyper-V switch extension...")
        from systemFolderPath in Environment<RT>.getFolderPath(Environment.SpecialFolder.System)
        from newLine in Environment<RT>.newLine
        let netCfgPath = Path.Combine(systemFolderPath, "netcfg.exe")
        let infFileName = Path.GetFileName(infPath)
        let infDirectoryPath = Path.GetDirectoryName(infPath)
        from result in ProcessRunner<RT>.runProcess(
            netCfgPath,
            @$"-l ""{infFileName}"" -c s -i {EryphConstants.DriverModuleName}",
            infDirectoryPath)
        from __ in guard(result.ExitCode == 0, Error.New($"Failed to install OVS Hyper-V switch extension:{newLine}{result.Output}"))
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from ___ in hostNetworkCommands.EnableSwitchExtension()
        from ____ in logInformation("Successfully installed OVS Hyper-V switch extension")
        select unit;

    public static Aff<RT, Unit> uninstallDriver() =>
        from _ in logInformation("Going to uninstall OVS Hyper-V switch extension...")
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from __ in hostNetworkCommands.DisableSwitchExtension()
        from systemFolderPath in Environment<RT>.getFolderPath(Environment.SpecialFolder.System)
        from newLine in Environment<RT>.newLine
        let netCfgPath = Path.Combine(systemFolderPath, "netcfg.exe")
        from result in ProcessRunner<RT>.runProcess(netCfgPath, $"/u {EryphConstants.DriverModuleName}")
        from ___ in guard(result.ExitCode == 0, Error.New($"Failed to uninstall OVS Hyper-V switch extension:{newLine}{result.Output}"))
        from ____ in logInformation("Successfully uninstalled OVS Hyper-V switch extension")
        select unit;

    public static Aff<RT, Unit> removeAllDriverPackages() =>
        from installedDriverPackages in getInstalledDriverPackages()
        from ___ in installedDriverPackages
            .Map(di => removeDriverPackage(di.Driver))
            .TraverseSerial(u => u)
        select unit;

    internal static Aff<RT, Unit> removeDriverPackage(string infName) =>
        from _ in logInformation("Going to remove driver package {InfName}...", infName)
        from result in ProcessRunner<RT>.runProcess("pnputil.exe", $"/delete-driver {infName} /uninstall /force")
        from newLine in Environment<RT>.newLine
        from __ in guard(result.ExitCode == 0, Error.New($"Failed to remove driver package {infName}:{newLine}{result.Output}"))
        from ___ in logInformation("Successfully removed driver package {InfName}", infName)
        select unit;

    public static Aff<RT, Seq<DismDriverInfo>> getInstalledDriverPackages() =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-WindowsDriver")
            .AddParameter("Online")
        from result in psEngine.GetObjectsAsync<DismDriverInfo>(command).ToAff()
        select result.Select(r => Optional(r.Value))
            .Somes()
            .Filter(di => di.OriginalFileName?.Contains(
                 EryphConstants.DriverModuleName, StringComparison.OrdinalIgnoreCase) ?? false);

    public static Aff<RT, bool> isDriverLoaded() =>
        from result in ProcessRunner<RT>.runProcess("driverquery.exe", "/FO LIST")
        from match in Eff(() => Regex.Match(
            result.Output,
            $@"Module Name:\s*{Regex.Escape(EryphConstants.DriverModuleName)}",
            RegexOptions.Multiline | RegexOptions.IgnoreCase))
        select match.Success;

    public static Aff<RT, Version> getDriverVersionFromInfFile(string filePath) =>
        from fileContent in getInfFileContent(filePath)
        from version in extractDriverVersionFromInf(fileContent)
        select version;

    internal static Aff<RT, string> getInfFileContent(string filePath) =>
        from bytes in File<RT>.readAllBytes(filePath)
        // INF files can be encoded in UTF-16 LE (preferred) or Windows code pages.
        // We detect the encoding by checking for the UTF-16 LE BOM.
        from content in Seq<byte>(0xFF, 0xFE) == bytes.Take(2).ToSeq()
            ? Eff<RT, string>(_ => Encoding.Unicode.GetString(bytes.Skip(2).ToArray()))
            // .NET Core does not support Windows code pages, so we fall back to ASCII.
            // Any INF files that use non-ASCII characters should hopefully be encoded
            // in UTF-16 LE anyway.
            : Eff<RT, string>(_ => Encoding.ASCII.GetString(bytes))
        select content;

    internal static Eff<Version> extractDriverVersionFromInf(string infContent) =>

        from match in Eff(() => Regex.Match(
            infContent,
            @"DriverVer\s*=.*,(\d+\.\d+\.\d+\.\d+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase))
        from _ in guard(match.Success, Error.New("Could not extract driver version from INF"))
        from version in parseVersion(match.Groups[1].Value).ToEff(Error.New("Could not parse driver version"))
        select version;

    private static Option<Version> parseVersion(string input) =>
        Version.TryParse(input, out var version) ? Some(version) : None;

    private static Eff<RT, Unit> logInformation(string message, params object[] args)
        => Logger<RT>.logInformation<OvsDriverProvider<RT>>(message, args);

    private static Eff<RT, Unit> logWarning(string message, params object[] args)
        => Logger<RT>.logWarning<OvsDriverProvider<RT>>(message, args);
}
