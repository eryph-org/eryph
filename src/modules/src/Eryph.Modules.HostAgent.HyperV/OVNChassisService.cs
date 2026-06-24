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

public class OVNChassisService(
    ISystemEnvironment systemEnvironment,
    ILogger<OVNChassisService> logger,
    IAgentControlService controlService,
    IOVSService<OVNChassisNode> ovnChassisNode,
    IOVSService<OVSDbNode> ovsDbNode,
    IOVSService<OVSSwitchNode> ovsVSwitchNode,
    IServiceProvider serviceProvider)
    : IHostedService
{
    private readonly ILogger _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        controlService.Register(this, OnControlEvent);
        await ovsDbNode.StartAsync(cancellationToken);

        StartOnOwnThread();
        await UpdateNetworkProviders();
        await ApplyChassisPlan(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        controlService.UnRegister(this);

        await Task.WhenAll(
            StopWitchCatch(ovnChassisNode, true, "Failed to stop OVN chassis node.", cancellationToken),
            DisconnectWitchCatch(ovsVSwitchNode, "Failed to stop vswitch node."),
            DisconnectWitchCatch(ovsDbNode, "Failed to stop chassis db node.")
        );
    }

    private async Task<bool> OnControlEvent(AgentControlEvent e, CancellationToken cancellationToken)
    {
        switch (e.Service)
        {
            case AgentService.OVNController:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await ovnChassisNode.StopAsync(true, cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await ovnChassisNode.StartAsync(cancellationToken);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            case AgentService.VSwitch:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await ovsVSwitchNode.StopAsync(true, cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await ovsVSwitchNode.StartAsync(cancellationToken);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            case AgentService.OVSDB:
                switch (e.RequestedOperation)
                {
                    case AgentServiceOperation.Stop:
                        await ovsDbNode.StopAsync(true, cancellationToken);
                        return true;
                    case AgentServiceOperation.Start:
                        await ovsDbNode.StartAsync(cancellationToken);
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        }

        return false;
    }

    private void StartOnOwnThread()
    {
        Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                try
                {
                    var extensionEnabled = await systemEnvironment
                        .GetOvsExtensionManager()
                        .IsExtensionEnabled();

                    if (!extensionEnabled.IfLeft(false))
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    var cancelSource = new CancellationTokenSource(30000);
                    await ovsVSwitchNode.StartAsync(cancelSource.Token);
                    cancelSource = new CancellationTokenSource(30000);
                    await ovnChassisNode.StartAsync(cancelSource.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to start OVN chassis");
                }

                break;
            }
        }, TaskCreationOptions.LongRunning);
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

    private async Task DisconnectWitchCatch<TNode>(IOVSService<TNode> service, string errorMessage)
        where TNode : IOVSNode
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
            await using var scope = AsyncScopedLifestyle.BeginScope(serviceProvider as Container);
            var providerManager = scope.GetInstance<INetworkProviderManager>();
            var configResult = await providerManager.GetCurrentConfiguration().ToEither();
            var config = configResult.Match(
                c => c,
                e =>
                {
                    _logger.LogWarning(
                        "Failed to load network provider configuration for OVN chassis plan: {Error}",
                        e.Message);
                    return null!;
                });
            if (config is null) return;

            var ovsTool = new OVSControlTool(systemEnvironment, LocalConnections.Switch);
            var realizer = new ChassisPlanRealizer(systemEnvironment, ovsTool);
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
        var runtime = AgentRuntime.New(serviceProvider);

        await using var scope = AsyncScopedLifestyle.BeginScope(serviceProvider as Container);

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
            _logger.LogCritical(ex,
                "Automatic configuration of network provider(s) failed. The networking might not work. "
                + "Please run 'eryph-zero networks sync' to resolve the issues.");
        }
    }
}
