using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Sys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

using static Console<DriverCommandsRuntime>;
using static OvsDriverProvider<DriverCommandsRuntime>;

internal static class DriverCommands
{
    public static Task<int> GetDriverStatus()
    {
        return AdminGuard.CommandIsElevated(async () =>
        {
            var result = await Run(
                from hostNetworkCommands in default(DriverCommandsRuntime).HostNetworkCommands
                from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
                let extensionMessage = extensionInfo.Match(
                    Some: ei => $"Hyper-V switch extension found: {ei.Name} {ei.Version}",
                    None: () => "Hyper-V switch extension not found")
                from _ in writeLine(extensionMessage)
                from isDriverLoaded in isDriverLoaded()
                from __ in writeLine(isDriverLoaded
                    ? "Hyper-V switch extension driver is loaded"
                    : "Hyper-V switch extension driver is not loaded. Overlay network might not work. Consider reinstalling the driver.")
                from isDriverServiceRunning in isDriverServiceRunning()
                from ___ in writeLine(isDriverServiceRunning
                    ? "Hyper-V switch extension driver service is running"
                    : "Hyper-V switch extension driver service is not running")
                from installedDriverPackages in getInstalledDriverPackages()
                from ____ in writeLine(installedDriverPackages.Fold(
                    $"The following driver packages are installed:",
                    (acc, info) =>
                        $"{acc}{Environment.NewLine}\t{info.Driver} - {info.Version} {info.OriginalFileName}"))
                from ovsRunDir in Eff(() => OVSPackage.UnpackAndProvide())
                let packageInfFile = Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf")
                from packageDriverVersion in getDriverVersionFromInfFile(
                    packageInfFile)
                from _____ in writeLine(
                    $"Driver version in OVS package: {packageDriverVersion}")
                from isDriverPackageTestSigned in
                    isDriverPackageTestSigned(
                        packageInfFile)
                from ______ in isDriverPackageTestSigned
                    ? writeLine($"Driver in OVS package is test signed")
                    : SuccessEff(unit)
                from isDriverTestSigningEnabled in isDriverTestSigningEnabled()
                from _______ in writeLine(
                    $"Driver test signing is {(isDriverTestSigningEnabled ? "" : "not ")}enabled")
                select unit);

            result.IfFail(err => Console.WriteLine(err.ToString()));
            return result.IsFail ? -1 : 0;
        });
    }

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

    public static Task<Fin<Unit>> RemoveDriver(
        ILoggerFactory loggerFactory)
    {
        return Run(
            from _ in uninstallDriver()
            from __ in removeAllDriverPackages()
            select unit,
            loggerFactory);
    }

    private static async Task<Fin<Unit>> Run(Aff<DriverCommandsRuntime, Unit> logic)
    {
        using var loggerFactory = new NullLoggerFactory();
        
        return await Run(logic, loggerFactory);
    }

    private static async Task<Fin<Unit>> Run(Aff<DriverCommandsRuntime, Unit> logic,
        ILoggerFactory loggerFactory)
    {
        using var psEngine = new PowershellEngine(loggerFactory.CreateLogger<PowershellEngine>());
        using var cts = new CancellationTokenSource();
        var runtime = new DriverCommandsRuntime(new(cts, loggerFactory, psEngine));

        return await logic.Run(runtime);
    }
}
