using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
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
    private readonly ISysEnvironment _sysEnvironment;
    private readonly IAgentControlService _controlService;
    private readonly IOVSService<OVNChassisNode> _ovnChassisNode;
    private readonly IOVSService<OVSDbNode> _ovsDbNode;
    private readonly IOVSService<OVSSwitchNode> _ovsVSwitchNode;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public OVSChassisService(
        ISysEnvironment sysEnvironment,
        ILogger<OVSChassisService> logger,
        IAgentControlService controlService, 
        IOVSService<OVNChassisNode> ovnChassisNode, 
        IOVSService<OVSDbNode> ovsDbNode, 
        IOVSService<OVSSwitchNode> ovsVSwitchNode, 
        IServiceProvider serviceProvider)
    {
        _sysEnvironment = sysEnvironment;
        this._controlService = controlService;
        _ovnChassisNode = ovnChassisNode;
        _ovsDbNode = ovsDbNode;
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
            case AgentService.OVSDB:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await _ovsDbNode.StopAsync(true, cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await _ovsDbNode.StartAsync(cancellationToken);
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
        await _ovsDbNode.StartAsync(cancellationToken);

        StartOnOwnThread();
        await UpdateNetworkProviders();

    }

    private void StartOnOwnThread()
    {
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                try
                {
                    var extensionEnabled = _sysEnvironment.GetOvsExtensionManager().IsExtensionEnabled();

                    if (!extensionEnabled)
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    var cancelSource = new CancellationTokenSource(30000);
                    await _ovsVSwitchNode.StartAsync(cancelSource.Token);
                    cancelSource = new CancellationTokenSource(30000);
                    await _ovnChassisNode.StartAsync(cancelSource.Token);

                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to start ovs chassis");
                }

                break;
            }
        }, TaskCreationOptions.LongRunning);




    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _controlService.UnRegister(this);

        await Task.WhenAll(
            StopWitchCatch(_ovnChassisNode, true, "Failed to stop OVN chassis node.", cancellationToken),
            DisconnectWitchCatch(_ovsVSwitchNode, "Failed to stop vswitch node."),
            DisconnectWitchCatch(_ovsDbNode, "Failed to stop chassis db node.")

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

    private async Task DisconnectWitchCatch<TNode>(IOVSService<TNode> service, string errorMessage) where TNode : IOVSNode
    {
        try
        {
            await service.DisconnectDemons();
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
