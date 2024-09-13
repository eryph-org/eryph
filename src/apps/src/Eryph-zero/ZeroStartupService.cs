using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Runtime.Zero.HttpSys;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

public sealed class ZeroStartupService(
    Container container,
    IEndpointResolver endpointResolver,
    ILogger logger) : IHostedService, IDisposable
{
    private SSLEndpointContext? _sslEndpointContext;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Zero startup service started.");

        await EnsureSystemClient();
        await EnableSslEndpoint();
        await EnsureHyperVAndOvs();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EnsureSystemClient()
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var systemClientGenerator = scope.GetInstance<ISystemClientGenerator>();
        await systemClientGenerator.EnsureSystemClient();
    }

    private async Task EnableSslEndpoint()
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var endpointManager = scope.GetInstance<ISSLEndpointManager>();
        var baseUrl = endpointResolver.GetEndpoint("base");

        _sslEndpointContext = await endpointManager.EnableSslEndpoint(new SSLOptions(
            "eryph-zero CA",
            Network.FQDN,
            DateTime.UtcNow.AddDays(-1),
            365 * 5,
            ZeroConfig.GetPrivateConfigPath(),
            "eryphCA",
            Guid.Parse("9412ee86-c21b-4eb8-bd89-f650fbf44931"),
            baseUrl));
    }

    private async Task EnsureHyperVAndOvs()
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var configuration = scope.GetInstance<IConfiguration>();
        var loggerFactory = scope.GetInstance<ILoggerFactory>();
        var ovsPathProvider = scope.GetInstance<IEryphOvsPathProvider>();
        var isWarmupMode = configuration.GetValue<bool>("warmupMode");
        var isWindowsService = WindowsServiceHelpers.IsWindowsService();
        var ovsPackageDirectory = configuration.GetValue<string?>("ovsPackagePath");

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

    public void Dispose()
    {
        _sslEndpointContext?.Dispose();
    }
}