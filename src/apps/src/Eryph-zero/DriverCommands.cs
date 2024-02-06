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
                from installedDriverPackages in OvsDriverProvider<DriverCommandsRuntime>.getInstalledDriverPackages()
                from newLine in Environment<DriverCommandsRuntime>.newLine
                from ___ in Console<DriverCommandsRuntime>.writeLine(installedDriverPackages.Fold(
                    $"The following driver packages are installed:",
                    (acc, info) => $"{acc}{newLine}\t{info.Driver} - {info.Version} {info.OriginalFileName}"))
                let ovsRunDir = Try(() => OVSPackage.UnpackAndProvide()).ToOption()
                from packageDriverVersion in match(ovsRunDir,
                    Some: d =>
                        from v in OvsDriverProvider<DriverCommandsRuntime>.getDriverVersionFromInfFile(Path.Combine(d, "driver", "dbo_ovse.inf"))
                        select Some(v),
                    None: () => SuccessAff((Option<Version>)None))
                from ____ in match(packageDriverVersion,
                    Some: v => Console<DriverCommandsRuntime>.writeLine($"Driver version in OVS package: {v}"),
                    None: () => Console<DriverCommandsRuntime>.writeLine("No OVS package found"))
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

        public static async Task<Fin<Unit>> Run(Aff<DriverCommandsRuntime, Unit> logic)
        {
            using var loggerFactory = new NullLoggerFactory();
            
            return await Run(logic, loggerFactory);
        }

        private static async Task<Fin<Unit>> Run(Aff<DriverCommandsRuntime, Unit> logic,
            ILoggerFactory loggerFactory)
        {
            using var psEngine = new PowershellEngine(loggerFactory.CreateLogger<PowershellEngine>());
            using var cts = new CancellationTokenSource();
            var runtime = new DriverCommandsRuntime(cts, loggerFactory, psEngine);

            return await logic.Run(runtime);
        }
    }
}
