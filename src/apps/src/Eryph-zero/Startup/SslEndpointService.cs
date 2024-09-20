using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore;
using Eryph.Runtime.Zero.HttpSys;
using Microsoft.Extensions.Hosting;

namespace Eryph.Runtime.Zero.Startup;

internal class SslEndpointService(
    IEndpointResolver endpointResolver,
    ISSLEndpointManager sslEndpointManager,
    ISSLEndpointRegistry sslEndpointRegistry)
    : IHostedService
{
    private readonly SslOptions _options = new(
        endpointResolver.GetEndpoint("base"),
        365 * 5,
        Guid.Parse("9412ee86-c21b-4eb8-bd89-f650fbf44931"),
        "eryph-zero-tls-key");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        sslEndpointManager.EnableSslEndpoint(_options);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        sslEndpointRegistry.UnRegisterSSLEndpoint(_options);
        return Task.CompletedTask;
    }
}
