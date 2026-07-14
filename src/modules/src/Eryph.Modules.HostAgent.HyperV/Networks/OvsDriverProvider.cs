using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Networks;

public class OvsDriverProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasDirectory<RT>,
    HasDism<RT>,
    HasFile<RT>,
    HasHostNetworkCommands<RT>,
    HasLogger<RT>,
    HasPowershell<RT>,
    HasProcessRunner<RT>,
    HasRegistry<RT>
{
    public static Aff<RT, Unit> ensureDriver(
        string ovnRunDir,
        string ovnDataDir,
        bool allowInstall,
        bool allowUpgrade) =>
        from hostNetworkCommands in default(RT).HostNetworkCommands
        from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
        from _ in match(extensionInfo,
            ei => logInformation("OVS Hyper-V switch extension {ExtensionVersion} is installed", ei.Version ?? ""),
            () => logInformation("OVS Hyper-V switch extension is not installed"))
        let infPath = Path.Combine(ovnRunDir, "driver", "dbo_ovse.inf")
        from infVersion in getDriverVersionFromInfFile(infPath)
        from isDriverTestSigningEnabled in isDriverTestSigningEnabled()
        from isDriverPackageTestSigned in isDriverPackageTestSigned(infPath)
        from __ in isDriverPackageTestSigned && !isDriverTestSigningEnabled
            ? logWarning(
                "Driver package is test signed but test signing is disabled in the OS. The driver will not be used.")
            : SuccessAff<RT, Unit>(unit)
        let canInstall = allowInstall && (!isDriverPackageTestSigned || isDriverTestSigningEnabled)
        let canUpgrade = allowUpgrade && (!isDriverPackageTestSigned || isDriverTestSigningEnabled)
        from ___ in match(extensionInfo,
            ei =>
                from extensionVersion in parseVersion(ei.Version ?? "").ToAff(Error.New(
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
                    // Stop any OVS/OVN daemons still running from the previous version.
                    // Stopping the eryph service does not stop them, and they keep the
                    // OVN/OVS database lock files open. Dropping the data directory below
                    // would then fail. Ask each daemon to exit gracefully and only
                    // force-terminate the ones that do not stop in time.
                    from _stopDaemons in stopRunningOvsDaemons(ovnRunDir, ovnDataDir)
                    // The OVN/OVS database schemas are tied to the binaries shipped in
                    // the package. On any version change we drop the databases so the new
                    // binaries start from a clean state. The network plan realizer rebuilds
                    // the OVN configuration from eryph's state on the next sync.
                    from _____ in dropOvnDatabaseFiles(ovnDataDir)
                    from ______ in installDriver(infPath)
                    from _______ in match(overlaySwitchId,
                        switchId =>
                            from _ in hostNetworkCommands.EnableSwitchExtension(switchId)
                            // We suspect that the switch extension might not be enabled
                            // immediately on slow systems
                            from __ in waitUntilSwitchExtensionIsEnabled(switchId)
                            select unit,
                        () => SuccessAff<RT, Unit>(unit))
                    select unit
                    : from _ in extensionVersion != infVersion
                        ? logWarning(
                            "Hyper-V switch extension version {ExtensionVersion} does not match packaged driver version {DriverVersion}",
                            ei.Version ?? "", infVersion)
                        : SuccessAff<RT, Unit>(unit)
                    select unit
                select unit,
            () => canInstall
                ? installDriver(infPath)
                : FailAff<RT, Unit>(Error.New("OVS Hyper-V switch extension is missing")))
        select unit;

    public static Aff<RT, Unit> dropOvnDatabaseFiles(string ovnDataDir) =>
        from exists in Directory<RT>.exists(ovnDataDir)
        from _ in exists
            ? from __ in logInformation("Dropping OVN database files at {Path}...", ovnDataDir)
            from ___ in Directory<RT>.delete(ovnDataDir)
            from ____ in logInformation("Successfully dropped OVN database files")
            select unit
            : logInformation("No OVN database files to drop at {Path}.", ovnDataDir)
        select unit;

    // On an upgrade, OVS/OVN daemons started by the previous installation keep running
    // (stopping the eryph service does not stop them). They hold the OVN/OVS database lock
    // files open, so dropOvnDatabaseFiles cannot delete the data directory. Send each daemon
    // the OVS 'exit' control command and wait for it to remove its pidfile, force-terminating
    // only those that do not stop within the timeout.
    public static Aff<RT, Unit> stopRunningOvsDaemons(string ovnRunDir, string ovnDataDir) =>
        from controlFiles in getDaemonFiles(ovnDataDir, "*.ctl")
        from _ in controlFiles.IsEmpty
            ? logInformation("No running OVS/OVN daemons found before the OVN update.").ToAff()
            : from _1 in logInformation(
                    "Stopping {Count} OVS/OVN daemon(s) left over from the previous version...",
                    controlFiles.Count)
                from _2 in controlFiles.Map(ctl => sendDaemonExit(ovnRunDir, ctl)).SequenceSerial()
                from _3 in waitUntilDaemonsStopped(ovnDataDir)
                from _4 in forceStopRemainingDaemons(ovnDataDir)
                select unit
        select unit;

    // The OVS/OVN daemons keep their control sockets (*.ctl) and pidfiles under
    // '<ovnDataDir>\var\run\{openvswitch,ovn}'. A running daemon has both; a clean exit
    // removes the pidfile.
    private static readonly Seq<string> DaemonRunSubDirectories = Seq("openvswitch", "ovn");

    private static string appctlPath(string ovnRunDir) =>
        Path.Combine(ovnRunDir, "usr", "bin", "ovs-appctl.exe");

    private static Aff<RT, Seq<string>> getDaemonFiles(string ovnDataDir, string searchPattern) =>
        DaemonRunSubDirectories
            .Map(subDir => enumerateExistingFiles(
                Path.Combine(ovnDataDir, "var", "run", subDir), searchPattern))
            .SequenceSerial()
            .Map(files => files.Flatten());

    private static Aff<RT, Seq<string>> enumerateExistingFiles(string directory, string searchPattern) =>
        from exists in Directory<RT>.exists(directory)
        from files in exists
            ? Directory<RT>.enumerateFiles(directory, searchPattern).ToAff()
            : SuccessAff<RT, Seq<string>>(Seq<string>())
        select files;

    private static Aff<RT, Unit> sendDaemonExit(string ovnRunDir, string controlFile) =>
        from result in ProcessRunner<RT>.runProcess(
                appctlPath(ovnRunDir),
                $"--timeout=5 -t \"{controlFile}\" exit",
                includeStandardError: true)
            | @catch(_ => SuccessAff<RT, ProcessRunnerResult>(
                new ProcessRunnerResult(-1, "The exit command could not be sent.")))
        from _ in result.ExitCode == 0
            ? logInformation("Requested shutdown of OVS/OVN daemon '{ControlFile}'.", controlFile)
            : logInformation(
                "OVS/OVN daemon '{ControlFile}' did not accept the exit command (it may have already stopped): {Output}",
                controlFile, result.Output.Trim())
        select unit;

    private static Aff<RT, Unit> waitUntilDaemonsStopped(string ovnDataDir) =>
        from _ in repeatWhile(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(2))
            & Schedule.upto(TimeSpan.FromSeconds(30)),
            from pidFiles in getDaemonFiles(ovnDataDir, "*.pid")
            from __ in pidFiles.IsEmpty
                ? SuccessEff<RT, Unit>(unit)
                : logInformation("Waiting for {Count} OVS/OVN daemon(s) to stop...", pidFiles.Count)
            select pidFiles,
            pidFiles => pidFiles.Count > 0)
        select unit;

    private static Aff<RT, Unit> forceStopRemainingDaemons(string ovnDataDir) =>
        from pidFiles in getDaemonFiles(ovnDataDir, "*.pid")
        from _ in pidFiles.Map(forceStopDaemon).SequenceSerial()
        select unit;

    private static Aff<RT, Unit> forceStopDaemon(string pidFile) =>
        from content in File<RT>.readAllText(pidFile)
            | @catch(_ => SuccessAff<RT, string>(""))
        from _ in parseInt(content.Trim()).Match(
            Some: pid =>
                from _1 in logWarning(
                    "OVS/OVN daemon (pid {Pid}) did not stop gracefully; terminating it.", pid)
                from result in ProcessRunner<RT>.runProcess(
                        "taskkill.exe", $"/PID {pid} /F /T", includeStandardError: true)
                    | @catch(ex => SuccessAff<RT, ProcessRunnerResult>(
                        new ProcessRunnerResult(-1, ex.Message)))
                // Do not fail here: a leftover pidfile often belongs to a daemon that
                // has already exited (taskkill then reports "process not found"), which
                // is exactly the outcome we want. Log the real result honestly instead of
                // implying success. If a daemon genuinely could not be killed it still
                // holds the database lock, and the subsequent dropOvnDatabaseFiles fails
                // loudly -- the warning below explains why.
                from _2 in result.ExitCode == 0
                    ? logInformation("Terminated OVS/OVN daemon (pid {Pid}).", pid)
                    : logWarning(
                        "Could not terminate OVS/OVN daemon (pid {Pid}); it may already have stopped, or it still holds the OVN/OVS database lock: {Output}",
                        pid, result.Output.Trim())
                select unit,
            None: () => SuccessAff<RT, Unit>(unit))
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
            .Map(di => removeDriverPackage(di.Driver ?? throw new InvalidOperationException("Driver package has no driver name")))
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
        // The result can be null. Hence, we cannot directly call HeadOrNone().
        from signer in powershellResult.Map(Optional).Somes().HeadOrNone()
            .ToEff(Error.New("Could not read signature from file"))
        select !signer.Contains("Microsoft Windows Hardware Compatibility Publisher",
            StringComparison.OrdinalIgnoreCase);

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
            RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)))
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
            RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)))
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
