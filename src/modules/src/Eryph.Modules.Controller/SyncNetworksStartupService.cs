using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller;

public class SyncNetworksStartupService(
    Container container,
     ILogger logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        logger.LogInformation("Syncing networks...");
        var networkSyncService = scope.GetInstance<INetworkSyncService>();
        await networkSyncService.SyncNetworks(cancellationToken)
            .IfLeft(l =>
            {
                logger.LogError(l, "Failed to sync networks on startup.");
            });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
