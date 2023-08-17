using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal class GeneticsRequestWatcherService : BackgroundService
{
    private readonly ILogger _log;
    private readonly IGeneRequestBackgroundQueue _backgroundQueue;

    public GeneticsRequestWatcherService(ILogger log, IGeneRequestBackgroundQueue backgroundQueue)
    {
        _log = log;
        _backgroundQueue = backgroundQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem =
                await _backgroundQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Error occurred executing {WorkItem}.", nameof(workItem));
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {

        await base.StopAsync(stoppingToken);
    }
}