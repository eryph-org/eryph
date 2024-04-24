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
using Microsoft.PowerShell;
using System.Management.Automation.Runspaces;
using Eryph.VmManagement.Sys;

namespace Eryph.Modules.VmHostAgent.Networks;

public class OvsDriverProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasDism<RT>,
    HasFile<RT>,
    HasHostNetworkCommands<RT>,
    HasLogger<RT>,
    HasPowershell<RT>,
    HasProcessRunner<RT>,
    HasRegistry<RT>
{
    public static Aff<RT, Unit> ensureDriver(string ovsRunDir, bool allowInstall, bool allowUpgrade) =>
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
        from _ in match(extensionInfo,
            Some: ei => logInformation("OVS Hyper-V switch extension {ExtensionVersion} is installed", ei.Version),
            None: () => logInformation("OVS Hyper-V switch extension is not installed"))
        let infPath = Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf")
        from infVersion in getDriverVersionFromInfFile(infPath)
        from isDriverTestSigningEnabled in isDriverTestSigningEnabled()
        from isDriverPackageTestSigned in isDriverPackageTestSigned(infPath)
        from __ in isDriverPackageTestSigned && ! isDriverTestSigningEnabled
            ? logWarning("Driver package is test signed but test signing is disabled in the OS. The driver will not be used.")
            : SuccessAff<RT, Unit>(unit)
        let canInstall = allowInstall && (!isDriverPackageTestSigned || isDriverTestSigningEnabled)
        let canUpgrade = allowUpgrade && (!isDriverPackageTestSigned || isDriverTestSigningEnabled)
        from ___ in match(extensionInfo,
            Some: ei =>
                from extensionVersion in parseVersion(ei.Version).ToAff(Error.New(
                    "Could not parse the version of the Hyper-V extension"))
                from _ in extensionVersion != infVersion && canUpgrade
                    ? from switchExtensions in hostNetworkCommands.GetSwitchExtensions()
                      // The Open vSwitch extension should only be enabled for the single
                      // overlay switch. Just in case, we disable the extension on all switches.
                      // Normally, there should be only one overlay switch. Otherwise, the network
                      // needs to be rebuilt.
                      let overlaySwitchId = switchExtensions
                          .Find(e => e.SwitchName == EryphConstants.OverlaySwitchName)
                          .Map(e => e.SwitchId)
                      from _ in switchExtensions
                          .Filter(e => e.Enabled)
                          .Map(e => hostNetworkCommands.DisableSwitchExtension(e.SwitchId))
                          .SequenceSerial()
                      from __ in uninstallDriver()
                      // Wait for the driver service to be stopped/removed. Otherwise, the
                      // installation of the new driver might fail with error code 0x80070430.
                      from ___ in waitUntilDriverServiceHasStopped()
                      from ____ in removeAllDriverPackages()
                      from _____ in installDriver(infPath)
                      from ______ in match(overlaySwitchId,
                          Some: switchId =>
                              from _ in hostNetworkCommands.EnableSwitchExtension(switchId)
                              // We suspect that the switch extension might not be enabled
                              // immediately on slow systems
                              from __ in waitUntilSwitchExtensionIsEnabled(switchId)
                              select unit,
                          None: () => SuccessAff<RT, Unit>(unit))
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
        let infFileName = Path.GetFileName(infPath)
        from infVersion in getDriverVersionFromInfFile(infPath)
        let infDirectoryPath = Path.GetDirectoryName(infPath)
        from result in ProcessRunner<RT>.runProcess(
            "netcfg.exe",
            @$"/l ""{infFileName}"" /c s /i {EryphConstants.DriverModuleName}",
            infDirectoryPath)
        from __ in guard(result.ExitCode == 0,
            Error.New($"Failed to install OVS Hyper-V switch extension:{Environment.NewLine}{result.Output}"))
        from ____ in logInformation("Successfully installed OVS Hyper-V switch extension {DriverVersion}", infVersion)
        select unit;

    public static Aff<RT, Unit> uninstallDriver() =>
        from _ in logInformation("Going to uninstall OVS Hyper-V switch extension...")
        from result in ProcessRunner<RT>.runProcess("netcfg.exe", $"/u {EryphConstants.DriverModuleName}")
        from ___ in guard(result.ExitCode == 0,
            Error.New($"Failed to uninstall OVS Hyper-V switch extension:{Environment.NewLine}{result.Output}"))
        from ____ in logInformation("Successfully uninstalled OVS Hyper-V switch extension")
        select unit;

    public static Aff<RT, Unit> removeAllDriverPackages() =>
        from installedDriverPackages in getInstalledDriverPackages()
        from ___ in installedDriverPackages
            .Map(di => removeDriverPackage(di.Driver))
            .SequenceSerial()
        select unit;

    internal static Aff<RT, Unit> removeDriverPackage(string infName) =>
        from _ in logInformation("Going to remove driver package {InfName}...", infName)
        // The /uninstall flag is not supported on Windows Server 2016
        from result in ProcessRunner<RT>.runProcess("pnputil.exe", $"/delete-driver {infName} /force")
        from __ in guard(result.ExitCode == 0,
            Error.New($"Failed to remove driver package {infName}:{Environment.NewLine}{result.Output}"))
        from ___ in logInformation("Successfully removed driver package {InfName}", infName)
        select unit;

    public static Aff<RT, bool> isDriverLoaded() =>
        from result in ProcessRunner<RT>.runProcess("driverquery.exe", "/FO LIST")
        from _ in guard(result.ExitCode == 0, Error.New("Could not query loaded drivers"))
        // The output of driverquery.exe is localized. Hence, we just search for the driver name.
        select result.Output.Contains(EryphConstants.DriverModuleName, StringComparison.OrdinalIgnoreCase);

    public static Aff<RT, Version> getDriverVersionFromInfFile(string filePath) =>
        from fileContent in getInfFileContent(filePath)
        from version in extractDriverVersionFromInf(fileContent)
        select version;

    public static Eff<RT, bool> isDriverTestSigningEnabled() =>
        from registryValue in Registry<RT>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
            "SystemStartOptions")
        from startupOptions in registryValue.ToEff(Error.New("Could not read system startup options"))
        from _ in guard(startupOptions is string, Error.New("Could not read system startup options"))
        select ((string)startupOptions).Contains("TESTSIGNING", StringComparison.OrdinalIgnoreCase);

    public static Aff<RT, bool> isDriverPackageTestSigned(string infPath) =>
        from psEngine in default(RT).Powershell
        // We assume that the security catalog has the same file name as the INF file
        let catPath = Path.ChangeExtension(infPath, ".cat")
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-AuthenticodeSignature")
            .AddParameter("FilePath", catPath)
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "SignerCertificate")
            .AddCommand("Select-Object")
            .AddParameter("ExpandProperty", "Subject")
        from powershellResult in psEngine.GetObjectValuesAsync<string?>(command).ToAff()
        from signer in powershellResult.Map(Optional).Somes().HeadOrNone()
            .ToEff(Error.New("Could not read signature from file"))
        select !signer.Contains("Microsoft Windows Hardware Compatibility Publisher", StringComparison.OrdinalIgnoreCase);

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

    internal static Aff<RT, Unit> waitUntilDriverServiceHasStopped() =>
        from _ in repeatWhile(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(5))
            & Schedule.upto(TimeSpan.FromMinutes(5)),
            from _ in logInformation("Checking if driver service has stopped...")
            from isRunning in isDriverServiceRunning()
            select isRunning,
            isRunning => isRunning)
        select unit;

    internal static Aff<RT, Unit> waitUntilSwitchExtensionIsEnabled(Guid switchId) =>
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from _ in repeatUntil(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(5))
            & Schedule.upto(TimeSpan.FromMinutes(5)),
            from _ in logInformation("Checking if OVS Hyper-V switch extension is enabled...")
            from extensionInfos in hostNetworkCommands.GetSwitchExtensions()
            select extensionInfos.Find(e => e.SwitchId == switchId)
                .Map(e => e.Enabled)
                .IfNone(false),
            isEnabled => isEnabled)
        select unit;

    public static Aff<RT, bool> isDriverServiceRunning() =>
        from processResult in ProcessRunner<RT>.runProcess("sc.exe", "query type=driver")
        from __ in guard(processResult.ExitCode == 0, Error.New("Could not query running driver services"))
        from match in Eff(() => Regex.Match(
            processResult.Output,
            @$"SERVICE_NAME:\s*{Regex.Escape(EryphConstants.DriverModuleName)}",
            RegexOptions.Multiline | RegexOptions.IgnoreCase))
        select match.Success;

    private static Option<Version> parseVersion(string input) =>
        Version.TryParse(input, out var version) ? Some(version) : None;

    private static Eff<RT, Unit> logInformation(string message, params object[] args)
        => Logger<RT>.logInformation<OvsDriverProvider<RT>>(message, args);

    private static Eff<RT, Unit> logWarning(string message, params object[] args)
        => Logger<RT>.logWarning<OvsDriverProvider<RT>>(message, args);

    public static Aff<RT, Seq<DismDriverInfo>> getInstalledDriverPackages() =>
        from allDriverPackages in Dism<RT>.getInstalledDriverPackages()
        select allDriverPackages.Filter(di => di.OriginalFileName?.Contains(
            EryphConstants.DriverModuleName, StringComparison.OrdinalIgnoreCase) ?? false);
}
