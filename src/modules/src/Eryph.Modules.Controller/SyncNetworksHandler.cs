using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Core;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.Modules.Controller;

public class SyncNetworksHandler : IHostedServiceHandler
{
    private readonly Container _container;
    private readonly ILogger _logger;

    public SyncNetworksHandler(Container container, ILogger<SyncNetworksHandler> logger)
    {
        _container = container;
        _logger = logger;
    }

    public Task Execute(CancellationToken stoppingToken)
    {
        return _container.GetInstance<INetworkSyncService>().SyncNetworks(stoppingToken)
            .IfLeft(l =>
            {
                _logger.LogError(l.ToException(), "Failed to sync networks on startup.");
            });
    }
}
