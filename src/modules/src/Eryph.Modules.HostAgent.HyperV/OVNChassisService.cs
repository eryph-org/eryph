using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.OVN.OSCommands.OVS;
using Eryph.Core;
using Eryph.Core.Network;
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

public class OVNChassisService : IHostedService
{
    private readonly ISystemEnvironment _systemEnvironment;
    private readonly IAgentControlService _controlService;
    private readonly IOVSService<OVNChassisNode> _ovnChassisNode;
    private readonly IOVSService<OVSDbNode> _ovsDbNode;
    private readonly IOVSService<OVSSwitchNode> _ovsVSwitchNode;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public OVNChassisService(
        ISystemEnvironment systemEnvironment,
        ILogger<OVNChassisService> logger,
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
        await ApplyChassisPlan(cancellationToken);

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
                    _logger.LogCritical(ex, "Failed to start OVN chassis");
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

    private async Task ApplyChassisPlan(CancellationToken cancellationToken)
    {
        // The local chassis must register itself in the OVS database
        // (system-id, ovn-remote, ovn-encap-*, ovn-bridge-mappings) so that
        // ovn-controller can connect to the southbound DB and so that the
        // network plan's gateway router ports can be bound to this chassis
        // via the matching ha_chassis_group on the controller side.
        try
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_serviceProvider as Container);
            var providerManager = scope.GetInstance<INetworkProviderManager>();
            var configResult = await providerManager.GetCurrentConfiguration().ToEither();
            var config = configResult.Match(
                Right: c => c,
                Left: e =>
                {
                    _logger.LogWarning(
                        "Failed to load network provider configuration for OVN chassis plan: {Error}",
                        e.Message);
                    return null!;
                });
            if (config is null) return;

            var ovsTool = new OVSControlTool(_systemEnvironment, LocalConnections.Switch);
            var realizer = new ChassisPlanRealizer(_systemEnvironment, ovsTool);
            var plan = BuildChassisPlan(config);

            var result = await realizer.ApplyChassisPlan(plan, cancellationToken).ToEither();
            result.IfLeft(e =>
                _logger.LogWarning("Failed to apply OVN chassis plan: {Error}", e.Message));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply OVN chassis plan.");
        }
    }

    internal static ChassisPlan BuildChassisPlan(NetworkProvidersConfiguration config) =>
        Optional(config.NetworkProviders).ToSeq()
            .Flatten()
            .Filter(p => p.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay)
            .Filter(p => !string.IsNullOrWhiteSpace(p.BridgeName))
            .Fold(
                new ChassisPlan(EryphConstants.Networking.LocalChassisName)
                    .AddGeneveTunnelEndpoint(IPAddress.Loopback),
                (plan, provider) => plan.AddBridgeMapping(provider.Name, provider.BridgeName!));

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
                       : VmManagement.Sys.Logger<AgentRuntime>.logWarning<OVNChassisService>(
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
            _logger.LogCritical(ex, "Automatic configuration of network provider(s) failed. The networking might not work. "
                                    + "Please run 'eryph-zero networks sync' to resolve the issues.");
        }

    }
}
