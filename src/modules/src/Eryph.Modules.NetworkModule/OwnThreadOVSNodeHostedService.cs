using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Network;

public class OwnThreadOVSNodeHostedService<TNode> : IHostedService
    where TNode : IOVSNode
{
    private readonly IOVSService<TNode> _ovsNodeService;
    private readonly ILogger<OwnThreadOVSNodeHostedService<TNode>> _logger;

    /// <summary>
    /// Creates a new hosted service for <typeparamref name="TNode"/>.
    /// </summary>
    /// <param name="ovsNodeService"></param>
    /// <param name="logger"></param>
    public OwnThreadOVSNodeHostedService(
        IOVSService<TNode> ovsNodeService, ILogger<OwnThreadOVSNodeHostedService<TNode>> logger)
    {
        _ovsNodeService = ovsNodeService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {

        StartOnOwnThread();
        return Task.CompletedTask;
    }

    private void StartOnOwnThread()
    {
        Task.Factory.StartNew(async () =>
        {
            try
            {
                await _ovsNodeService.StartAsync(CancellationToken.None);

            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to start ovn service {serviceType}", _ovsNodeService.GetType());
                throw;
            }

        }, TaskCreationOptions.LongRunning);

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopWitchCatch(_ovsNodeService, true, 
            $"Failed to stop OVN service {_ovsNodeService.GetType()}", cancellationToken);


    }

    private async Task StopWitchCatch(IOVSService<TNode> service, bool ensureNodeStopped, string errorMessage
        , CancellationToken cancellationToken)
    {
        try
        {
            await service.StopAsync(ensureNodeStopped, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, errorMessage);
        }
    }




}