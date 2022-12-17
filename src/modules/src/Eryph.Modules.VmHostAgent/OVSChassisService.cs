using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.Modules.VmHostAgent.Networks;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static Eryph.Modules.VmHostAgent.Networks.NetworkProviderManager<Eryph.Modules.VmHostAgent.Networks.AgentRuntime>;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.VmHostAgent.Networks.AgentRuntime>;
using static LanguageExt.Prelude;


namespace Eryph.Modules.VmHostAgent;

public class OVSChassisService : IHostedService
{
    private readonly IAgentControlService _controlService;
    private readonly IOVSService<OVNChassisNode> _ovnChassisNode;
    private readonly IOVSService<OVSDbNode> _ovsDBNode;
    private readonly IOVSService<OVSSwitchNode> _ovsVSwitchNode;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public OVSChassisService(
        ILogger<OVSChassisService> logger,
        IAgentControlService controlService, 
        IOVSService<OVNChassisNode> ovnChassisNode, 
        IOVSService<OVSDbNode> ovsDbNode, 
        IOVSService<OVSSwitchNode> ovsVSwitchNode, 
        IServiceProvider serviceProvider)
    {
        this._controlService = controlService;
        _ovnChassisNode = ovnChassisNode;
        _ovsDBNode = ovsDbNode;
        _ovsVSwitchNode = ovsVSwitchNode;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private async Task<bool> OnControlEvent(AgentControlEvent e, CancellationToken cancellationToken)
    {

        switch (e.Service)
        {
            case AgentService.OVNController:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await _ovnChassisNode.StopAsync(true,cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await _ovnChassisNode.StartAsync(cancellationToken);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            case AgentService.VSwitch:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await _ovsVSwitchNode.StopAsync(true,cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await _ovsVSwitchNode.StartAsync(cancellationToken);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        }

        return false;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _controlService.Register(this, OnControlEvent);

        await _ovsDBNode.StartAsync(cancellationToken);
        await _ovsVSwitchNode.StartAsync(cancellationToken);
        await _ovnChassisNode.StartAsync(cancellationToken);

        await UpdateNetworkProviders();

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _controlService.UnRegister(this);

        await Task.WhenAll(
            StopWitchCatch(_ovnChassisNode,true, "Failed to stop OVN chassis node.", cancellationToken),
            StopWitchCatch(_ovsVSwitchNode, false, "Failed to stop vswitch node.", cancellationToken),
            StopWitchCatch(_ovsDBNode, false, "Failed to stop chassis db node.", cancellationToken)

        );

    }

    private async Task StopWitchCatch<TNode>(IOVSService<TNode> service, bool ensureNodeStopped, string errorMessage
        , CancellationToken cancellationToken) where TNode : IOVSNode
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

    private async Task UpdateNetworkProviders()
    {
        var runtime = AgentRuntime.New(_serviceProvider);

        await using var scope = AsyncScopedLifestyle.BeginScope(_serviceProvider as Container);

        try
        {
            await (from currentConfig in getCurrentConfiguration()
                   from hostState in getHostState()
                   from currentConfigChanges in generateChanges(hostState, currentConfig)
                   from _ in canBeAutoApplied(currentConfigChanges)
                       ? executeChanges(currentConfigChanges)
                       : Networks.Logger<AgentRuntime>.logWarning<OVSChassisService>(
                           "Network provider configuration is not fully applied to host." +
                           "\nSome of the required changes cannot be executed automatically." +
                           "\nRun command 'eryph-zero networks providers sync' in a elevated command prompt " +
                           "to apply changes." +
                           "\nChanges: {changes} ", currentConfigChanges.Operations.Select(x => x.Text))
                   select unit)

                .RunUnit(runtime);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure in automatic network provider update.");
        }

    }

}
