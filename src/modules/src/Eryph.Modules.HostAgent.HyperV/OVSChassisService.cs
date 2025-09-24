using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.HostAgent.Networks;
using LanguageExt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static Eryph.Modules.HostAgent.Networks.NetworkProviderManager<Eryph.Modules.HostAgent.Networks.AgentRuntime>;
using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.HostAgent.Networks.AgentRuntime>;
using static LanguageExt.Prelude;


namespace Eryph.Modules.HostAgent;

public class OVSChassisService : IHostedService
{
    private readonly ISystemEnvironment _systemEnvironment;
    private readonly IAgentControlService _controlService;
    private readonly IOVSService<OVNChassisNode> _ovnChassisNode;
    private readonly IOVSService<OVSDbNode> _ovsDbNode;
    private readonly IOVSService<OVSSwitchNode> _ovsVSwitchNode;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public OVSChassisService(
        ISystemEnvironment systemEnvironment,
        ILogger<OVSChassisService> logger,
        IAgentControlService controlService, 
        IOVSService<OVNChassisNode> ovnChassisNode, 
        IOVSService<OVSDbNode> ovsDbNode, 
        IOVSService<OVSSwitchNode> ovsVSwitchNode, 
        IServiceProvider serviceProvider)
    {
        _systemEnvironment = systemEnvironment;
        _controlService = controlService;
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
                    var extensionEnabled = await _systemEnvironment
                        .GetOvsExtensionManager()
                        .IsExtensionEnabled();

                    if (!extensionEnabled.IfLeft(false))
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
                   from hostState in HostStateProvider<AgentRuntime>.getHostState()
                   from currentConfigChanges in generateChanges(hostState, currentConfig, true)
                   from _1 in canBeAutoApplied(currentConfigChanges)
                       ? executeChangesWithRollback(currentConfigChanges)
                       : VmManagement.Sys.Logger<AgentRuntime>.logWarning<OVSChassisService>(
                           "Network provider configuration is not fully applied to host." +
                           "\nSome of the required changes cannot be executed automatically." +
                           "\nRun command 'eryph-zero networks sync' in a elevated command prompt " +
                           "to apply changes." +
                           "\nChanges: {changes} ", currentConfigChanges.Operations.Select(x => x.Text))
                   from _2 in HostStateProvider<AgentRuntime>.checkHostInterfaces()
                   select unit)

                .RunUnit(runtime);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure in automatic network provider update.");
        }

    }
}
