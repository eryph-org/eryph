using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.HttpSys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero;

public class ZeroStartupService(
    Container container,
    IEndpointResolver endpointResolver,
    ILogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Zero startup service started.");

        await EnableSslEndpoint();


        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EnableSslEndpoint()
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var endpointResolver = scope.GetInstance<IEndpointResolver>();
        var endpointManager = scope.GetInstance<ISSLEndpointManager>();
        var baseUrl = endpointResolver.GetEndpoint("base");

        await endpointManager.EnableSslEndpoint(new SSLOptions(
            "eryph-zero CA",
            Network.FQDN,
            DateTime.UtcNow.AddDays(-1),
            365 * 5,
            ZeroConfig.GetPrivateConfigPath(),
            "eryphCA",
            Guid.Parse("9412ee86-c21b-4eb8-bd89-f650fbf44931"),
            baseUrl));
    }
}