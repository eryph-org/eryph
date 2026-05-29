using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Logging;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Networks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Startup;

internal class EnsureHyperVAndOvnStartupHandler(
    IConfiguration configuration,
    IEryphOvnPathProvider ovnPathProvider,
    ILoggerFactory loggerFactory,
    ILogger<EnsureHyperVAndOvnStartupHandler> logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var isWarmupMode = configuration.GetValue<bool>("warmupMode");
        var isWindowsService = WindowsServiceHelpers.IsWindowsService();
        var ovnPackageDirectory = configuration.GetValue<string?>("ovnPackagePath");

        using var _ = logger.BeginWarmupProgressScope();

        var result = await DriverCommands.Run(
            EnsureHyperVAndOvn(isWindowsService, isWarmupMode, ovnPackageDirectory),
            loggerFactory);

        var ovnRunDir = result.ThrowIfFail();
        ovnPathProvider.SetOvnRunPath(ovnRunDir);
    }

    private static Aff<DriverCommandsRuntime, string> EnsureHyperVAndOvn(
        bool isWindowsService,
        bool isWarmupMode,
        string? ovnPackageDir) =>
        from _1 in SystemRequirementsChecker<DriverCommandsRuntime>.ensureHyperV(isWindowsService)
            .MapFail(e => Error.New(
                -10,
                "Hyper-V is not available. Make sure it is installed and the management service (VMMS) is running.",
                e))
        from ovnLogger in default(DriverCommandsRuntime).Logger<OVNPackage>()
        from runDir in Eff(() => OVNPackage.UnpackAndProvide(
                ovnLogger,
                ovnPackageDir))
            .ToAff()
            .MapFail(e => Error.New(-12, "Could not extract the included OVN distribution.", e))
        from _2 in OvsDriverProvider<DriverCommandsRuntime>.ensureDriver(
                runDir,
                OVNPackage.GetOvnDataPath(),
                !isWindowsService && !isWarmupMode,
                !isWindowsService && !isWarmupMode)
            .MapFail(e => Error.New(-11, "The Hyper-V switch extension for OVS is not available.", e))
        select runDir;
}
