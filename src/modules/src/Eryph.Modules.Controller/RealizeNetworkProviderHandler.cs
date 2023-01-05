using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Core;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.Modules.Controller;

public class RealizeNetworkProviderHandler : IHostedServiceHandler
{
    private readonly Container _container;
    private readonly ILogger _logger;

    public RealizeNetworkProviderHandler(Container container, ILogger<RealizeNetworkProviderHandler> logger)
    {
        _container = container;
        _logger = logger;
    }

    public Task Execute(CancellationToken stoppingToken)
    {
        return _container.GetInstance<INetworkSyncService>().RealizeProviderNetworks(stoppingToken)
            .IfLeft(l =>
            {
                _logger.LogError("Failed to generate networks. Message: {message}", l.Message);
                _logger.LogDebug("Failed to generate networks. Error: {@error}", l);

            });
    }
}