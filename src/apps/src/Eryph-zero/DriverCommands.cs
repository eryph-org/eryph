using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Sys;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero
{
    internal static class DriverCommands
    {
        public static async Task<int> GetDriverStatus()
        {
            var result = await Run(
                from hostNetworkCommands in default(DriverCommandsRuntime).HostNetworkCommands
                from extensionInfo in hostNetworkCommands.GetInstalledSwitchExtension()
                let extensionMessage = extensionInfo.Match(
                    Some: ei => $"Hyper-V switch extension found: {ei.Name} {ei.Version}",
                    None: () => "Hyper-V switch extension not found")
                from _ in Console<DriverCommandsRuntime>.writeLine(extensionMessage)
                from isDriverLoaded in OvsDriverProvider<DriverCommandsRuntime>.isDriverLoaded()
                from __ in Console<DriverCommandsRuntime>.writeLine(isDriverLoaded
                    ? "Hyper-V switch extension driver is loaded"
                    : "Hyper-V switch extension driver is not loaded. Overlay network might not work. Consider reinstalling the driver.")
                from isDriverServiceRunning in OvsDriverProvider<DriverCommandsRuntime>.isDriverServiceRunning()
                from ___ in Console<DriverCommandsRuntime>.writeLine(isDriverServiceRunning
                    ? "Hyper-V switch extension driver service is running"
                    : "Hyper-V switch extension driver service is not running")
                from installedDriverPackages in OvsDriverProvider<DriverCommandsRuntime>.getInstalledDriverPackages()
                from ____ in Console<DriverCommandsRuntime>.writeLine(installedDriverPackages.Fold(
                    $"The following driver packages are installed:",
                    (acc, info) => $"{acc}{Environment.NewLine}\t{info.Driver} - {info.Version} {info.OriginalFileName}"))
                let packageInfFile = Try(() => OVSPackage.UnpackAndProvide()).ToOption()
                    .Map(ovsRunDir => Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf"))
                from _____ in match(packageInfFile,
                    Some: infFile =>
                        from packageDriverVersion in OvsDriverProvider<DriverCommandsRuntime>.getDriverVersionFromInfFile(infFile)
                        from _ in Console<DriverCommandsRuntime>.writeLine($"Driver version in OVS package: {packageDriverVersion}")
                        from isDriverPackageTestSigned in OvsDriverProvider<DriverCommandsRuntime>.isDriverPackageTestSigned(infFile)
                        from __ in isDriverPackageTestSigned
                            ? Console<DriverCommandsRuntime>.writeLine($"Driver in OVS package is test signed")
                            : SuccessEff(unit)
                        select unit,
                    None: () => Console<DriverCommandsRuntime>.writeLine("No OVS package found"))
                from isDriverTestSigningEnabled in OvsDriverProvider<DriverCommandsRuntime>.isDriverTestSigningEnabled()
                from ______ in Console<DriverCommandsRuntime>.writeLine(
                    $"Driver test signing is {(isDriverTestSigningEnabled ? "" : "not ")}enabled")
                select unit);

            result.IfFail(err => Console.WriteLine(err.ToString()));
            return result.IsFail ? -1 : 0;
        }

        public static Task<Fin<Unit>> EnsureDriver(
            string ovsRunDir,
            bool canInstall,
            bool canUpgrade,
            ILoggerFactory loggerFactory)
        {
            return Run(OvsDriverProvider<DriverCommandsRuntime>.ensureDriver(
                ovsRunDir, canInstall, canUpgrade),
                loggerFactory);
        }

        public static Task<Fin<Unit>> RemoveDriver(
            ILoggerFactory loggerFactory)
        {
            return Run(
                from _ in OvsDriverProvider<DriverCommandsRuntime>.uninstallDriver()
                from __ in OvsDriverProvider<DriverCommandsRuntime>.removeAllDriverPackages()
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
}
