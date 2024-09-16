using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.ModuleCore.Startup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller;

public class SyncNetworksHandler(
    INetworkSyncService networkSyncService,
     ILogger logger)
    : IStartupHandler
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Syncing networks...");
        await networkSyncService.SyncNetworks(cancellationToken)
            .IfLeft(l =>
            {
                logger.LogError(l, "Failed to sync networks on startup.");
            });
    }
}
