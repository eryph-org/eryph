using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eryph.AnsiConsole.Sys;
using Eryph.Modules.HostAgent.Networks;
using Eryph.VmManagement;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

using static AnsiConsole<DriverCommandsRuntime>;
using static OvsDriverProvider<DriverCommandsRuntime>;

internal static class DriverCommands
{
    public static Aff<DriverCommandsRuntime, Unit> GetDriverStatus() =>
        from hostNetworkCommands in default(DriverCommandsRuntime).HostNetworkCommands
        from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
        let extensionMessage = extensionInfo.Match(
            Some: ei => $"Hyper-V switch extension found: {ei.Name} {ei.Version}",
            None: () => "Hyper-V switch extension not found")
        from _1 in writeLine(extensionMessage)
        from isDriverLoaded in isDriverLoaded()
        from _2 in writeLine(isDriverLoaded
            ? "Hyper-V switch extension driver is loaded"
            : "Hyper-V switch extension driver is not loaded. Overlay network might not work. Consider reinstalling the driver.")
        from isDriverServiceRunning in isDriverServiceRunning()
        from _3 in writeLine(isDriverServiceRunning
            ? "Hyper-V switch extension driver service is running"
            : "Hyper-V switch extension driver service is not running")
        from installedDriverPackages in getInstalledDriverPackages()
        from _4 in write(new Rows([
                new Text("The following driver packages are installed:"),
                ..installedDriverPackages.Map(p => new Padder(
                    new Text($"{p.Driver} - {p.Version} {p.OriginalFileName}"),
                    new Padding(2, 0, 0 ,0)))
            ]))
        from ovsPackageLogger in default(DriverCommandsRuntime).Logger<OVSPackage>()
        from ovsRunDir in Eff(() => OVSPackage.UnpackAndProvide(ovsPackageLogger))
        let packageInfFile = Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf")
        from packageDriverVersion in getDriverVersionFromInfFile(
            packageInfFile)
        from _5 in writeLine(
            $"Driver version in OVS package: {packageDriverVersion}")
        from isDriverPackageTestSigned in
            isDriverPackageTestSigned(
                packageInfFile)
        from _6 in isDriverPackageTestSigned
            ? writeLine($"Driver in OVS package is test signed")
            : SuccessEff(unit)
        from isDriverTestSigningEnabled in isDriverTestSigningEnabled()
        from _7 in writeLine(
            $"Driver test signing is {(isDriverTestSigningEnabled ? "" : "not ")}enabled")
        select unit;

    public static Task<Fin<Unit>> EnsureDriver(
        string ovsRunDir,
        bool canInstall,
        bool canUpgrade,
        ILoggerFactory loggerFactory)
    {
        return Run(ensureDriver(
            ovsRunDir, canInstall, canUpgrade),
            loggerFactory);
    }

    public static async Task<Fin<T>> Run<T>(
        Aff<DriverCommandsRuntime, T> logic,
        ILoggerFactory loggerFactory)
    {
        using var psEngineLock = new PowershellEngineLock();
        using var psEngine = new PowershellEngine(
            loggerFactory.CreateLogger<PowershellEngine>(),
            psEngineLock);
        using var cts = new CancellationTokenSource();
        var runtime = new DriverCommandsRuntime(new(cts, loggerFactory, psEngine));

        return await logic.Run(runtime);
    }
}
