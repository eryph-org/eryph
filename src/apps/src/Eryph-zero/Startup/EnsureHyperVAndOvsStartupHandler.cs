using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Logging;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Networks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Startup;

internal class EnsureHyperVAndOvsStartupHandler(
    IConfiguration configuration,
    IEryphOvsPathProvider ovsPathProvider,
    ILoggerFactory loggerFactory,
    ILogger<EnsureHyperVAndOvsStartupHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var isWarmupMode = configuration.GetValue<bool>("warmupMode");
        var isWindowsService = WindowsServiceHelpers.IsWindowsService();
        var ovsPackageDirectory = configuration.GetValue<string?>("ovsPackagePath");

        using var _ = logger.BeginWarmupProgressScope();
        
        var result = await DriverCommands.Run(
            EnsureHyperVAndOvs(isWindowsService, isWarmupMode, ovsPackageDirectory),
            loggerFactory);

        var ovsRunDir = result.ThrowIfFail();
        ovsPathProvider.SetOvsRunPath(ovsRunDir);
    }

    private static Aff<DriverCommandsRuntime, string> EnsureHyperVAndOvs(
        bool isWindowsService,
        bool isWarmupMode,
        string? ovsPackageDir) =>
        from _1 in SystemRequirementsChecker<DriverCommandsRuntime>.ensureHyperV(isWindowsService)
            .MapFail(e => Error.New(
                -10,
                "Hyper-V is not available. Make sure it is installed and the management service (VMMS) is running.",
                e))
        from ovsLogger in default(DriverCommandsRuntime).Logger<OVSPackage>()
        from runDir in Eff(() => OVSPackage.UnpackAndProvide(
                ovsLogger,
                ovsPackageDir))
            .ToAff()
            .MapFail(e => Error.New(-12, "Could not extract the included OVS/OVN distribution.", e))
        from _2 in OvsDriverProvider<DriverCommandsRuntime>.ensureDriver(
                runDir, !isWindowsService && !isWarmupMode, !isWindowsService && !isWarmupMode)
            .MapFail(e => Error.New(-11, "The Hyper-V switch extension for OVS is not available.", e))
        select runDir;
}
