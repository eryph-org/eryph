using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Genetics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class GeneticsRequestWatcherService(
    Container container,
    IGeneRequestRegistry geneRequestRegistry,
    ILogger logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (geneId, geneHash) = await geneRequestRegistry.DequeueGeneRequest(stoppingToken);
            try
            {
                await ProcessRequest(geneId, geneHash, stoppingToken);
            }
            catch (Exception ex)
            {
                // Any exceptions should already have been handled. This is
                // just a safety net as an unhandled exception would stop
                // the background service and prevent further processing.
                logger.LogError(ex, "Failed to process request for gene {GeneId} ({GeneHash})",
                    geneId, geneHash);
            }
        }
    }

    private async Task ProcessRequest(
        UniqueGeneIdentifier geneId,
        GeneHash geneHash,
        CancellationToken cancellationToken)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var geneProvider = scope.GetInstance<IGeneProvider>();
        var result = await geneProvider.ProvideGene(
            geneId,
            geneHash,
            async (message, progress) => await geneRequestRegistry.ReportProgress(geneId, geneHash, message, progress))
            .RunWithCancel(cancellationToken);
        
        await geneRequestRegistry.CompleteRequest(geneId, geneHash, result);
    }
}