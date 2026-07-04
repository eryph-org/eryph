using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.OVN.OSCommands.OVS;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
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
    DistributedEndpointResolver endpointResolver,
    IServiceProvider serviceProvider)
    : IHostedService
{
    private readonly ILogger _logger = logger;

    // Serializes the startup apply against re-applies triggered by a distributed endpoint change, so a
    // change arriving mid-apply cannot interleave two chassis plans against OVS.
    private readonly SemaphoreSlim _applyLock = new(1, 1);

    // Lifetime token for background re-applies (endpoint-change driven), cancelled on stop.
    private readonly CancellationTokenSource _stopping = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        controlService.Register(this, OnControlEvent);
        await ovsDbNode.StartAsync(cancellationToken);

        StartOnOwnThread();
        await UpdateNetworkProviders();

        // Subscribe before the first apply so a change distributed while it runs is not lost — the
        // re-apply serializes behind the startup apply on _applyLock. The southbound endpoint is
        // distributed over the Endpoints config domain and may only arrive after startup.
        endpointResolver.Changed += OnEndpointsChanged;
        await ApplyChassisPlan(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop new re-applies (unsubscribe) and cancel any running one.
        endpointResolver.Changed -= OnEndpointsChanged;
        await _stopping.CancelAsync();
        controlService.UnRegister(this);

        // Drain an in-flight re-apply BEFORE tearing down the OVS nodes, so an apply cannot reconfigure
        // OVS while it is being stopped. The cancelled token makes any in-flight apply finish promptly.
        // Bound the wait by the shutdown token: if it trips first, skip the drain and disposal (a
        // harmless leak at process exit) rather than hang host shutdown on a stuck apply.
        var drained = false;
        try
        {
            await _applyLock.WaitAsync(cancellationToken);
            drained = true;
        }
        catch (OperationCanceledException)
        {
            // fall through to teardown without the lock
        }

        await Task.WhenAll(
            StopWitchCatch(ovnChassisNode, true, "Failed to stop OVN chassis node.", cancellationToken),
            DisconnectWitchCatch(ovsVSwitchNode, "Failed to stop vswitch node."),
            DisconnectWitchCatch(ovsDbNode, "Failed to stop chassis db node.")
        );

        // Hold the lock through disposal (never release it first) so no re-apply can slip in between a
        // release and the dispose. Only dispose if we actually acquired it.
        if (drained)
        {
            _stopping.Dispose();
            _applyLock.Dispose();
        }
    }

    // A distributed endpoint change may add/replace the OVN southbound endpoint after the startup
    // apply already ran (on the local pipe). Re-apply in the background; failures are logged, not
    // propagated to the bus/config-apply path that raised the event.
    private void OnEndpointsChanged(object? sender, EventArgs e) => _ = ReapplyChassisPlan();

    private async Task ReapplyChassisPlan()
    {
        try
        {
            await ApplyChassisPlan(_stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to re-apply OVN chassis plan after an endpoint change.");
        }
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
        await _applyLock.WaitAsync(cancellationToken);
        try
        {
            await ApplyChassisPlanCore(cancellationToken);
        }
        finally
        {
            _applyLock.Release();
        }
    }

    private async Task ApplyChassisPlanCore(CancellationToken cancellationToken)
    {
        // The local chassis must register itself in the OVS database
        // (system-id, ovn-remote, ovn-encap-*, ovn-bridge-mappings) so that
        // ovn-controller can connect to the southbound DB and so that the
        // network plan's gateway router ports can be bound to this chassis
        // via the matching ha_chassis_group on the controller side.
        try
        {
            var container = serviceProvider as Container
                ?? throw new InvalidOperationException("serviceProvider is not a SimpleInjector Container.");
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
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

            // When a standalone network process runs on a different host it advertises its southbound
            // database over the Endpoints config domain; ovn-controller must dial that endpoint over SSL
            // instead of the local pipe. Absent (co-located / eryph-zero), the chassis keeps the local
            // pipe and the loopback tunnel endpoint.
            var southbound = await ResolveSouthbound(container, cancellationToken);
            var encapIp = await ResolveEncapIp(scope);

            if (southbound is not null && IPAddress.IsLoopback(encapIp))
                _logger.LogWarning(
                    "The OVN southbound database is remote but no overlay transport IP is configured "
                    + "(agentsettings 'ovn.overlay_transport_ip'); Geneve tunnels use the loopback address "
                    + "and will not reach other hosts. Set 'ovn.overlay_transport_ip' to this host's "
                    + "overlay IP address.");

            var ovsTool = new OVSControlTool(systemEnvironment, LocalConnections.Switch);
            var realizer = new ChassisPlanRealizer(systemEnvironment, ovsTool);
            var plan = BuildChassisPlan(config, encapIp, southbound);

            var result = await realizer.ApplyChassisPlan(plan, cancellationToken).ToEither();
            result.IfLeft(e =>
                _logger.LogWarning("Failed to apply OVN chassis plan: {Error}", e.Message));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown / cancelled re-apply — stop quietly rather than logging a warning.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply OVN chassis plan.");
        }
    }

    // Resolves the remote southbound endpoint to dial, or null to keep the local pipe. Null when no
    // southbound endpoint is advertised (co-located), when it is malformed, when the agent is not
    // enrolled (no client certificate for SSL yet), or when the host does not resolve — the failures
    // log and the chassis re-applies on the next endpoint change or restart.
    private async Task<ChassisSouthbound?> ResolveSouthbound(
        Container container, CancellationToken cancellationToken)
    {
        if (!endpointResolver.TryGetRawEndpoint(OvnRemoteEndpoints.SouthboundName, out var endpoint))
            return null;

        (string Host, int Port) parsed;
        try
        {
            parsed = OvnRemoteEndpoints.ParseSslEndpoint(endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Ignoring malformed OVN southbound endpoint '{Endpoint}'.", endpoint);
            return null;
        }

        var certStore = container.GetRegistration(typeof(IComponentCertificateStore))?.GetInstance()
            as IComponentCertificateStore;
        var pem = certStore?.ReadClientCertificatePem();
        if (pem is null)
        {
            _logger.LogWarning(
                "The OVN southbound database is advertised at '{Endpoint}' but this agent has no enrolled "
                + "certificate, so ovn-controller cannot connect over SSL. The chassis stays on the local "
                + "pipe until it re-applies with a certificate present.", endpoint);
            return null;
        }

        // ovn-controller (OVS on Windows) cannot resolve a host name for an active 'ssl:' stream — it
        // fails with "address family not supported". So resolve the advertised host to an IP literal
        // here and configure ovn-remote with that. OVS validates the southbound server by certificate
        // chain, not host name, so dialing by IP does not weaken authentication.
        var address = await ResolveToIp(parsed.Host, cancellationToken);
        if (address is null)
        {
            _logger.LogWarning(
                "Could not resolve the OVN southbound host '{Host}' to an IP address; the chassis stays "
                + "on the local pipe and re-applies on the next change.", parsed.Host);
            return null;
        }

        return new ChassisSouthbound(address, parsed.Port, pem);
    }

    // Resolves a host to an IP literal for ovn-remote (OVS on Windows needs a literal). An IP literal
    // (including a bracketed IPv6 host) passes through unchanged; a name is resolved via DNS, preferring
    // IPv4 for the southbound tunnel underlay but accepting any resolved address. Returns null when the
    // host does not resolve, so the caller degrades to the local pipe rather than failing the apply.
    internal static async Task<string?> ResolveToIp(string host, CancellationToken cancellationToken)
    {
        var literal = host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
        if (IPAddress.TryParse(literal, out var parsed))
            return FormatForRemote(parsed);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Any resolution failure — NXDOMAIN, a transient DNS outage (SocketException), or a
            // syntactically invalid / over-long host name (ArgumentException) — is treated as
            // "unresolved" so the chassis keeps the local pipe and re-applies on the next change,
            // instead of aborting the whole chassis-plan apply. Only genuine cancellation propagates.
            return null;
        }

        var address = System.Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork)
                      ?? (addresses.Length > 0 ? addresses[0] : null);
        return address is null ? null : FormatForRemote(address);
    }

    // ovn-remote is an 'ssl:host:port' string, so an IPv6 literal must be bracketed to stay unambiguous
    // with the port separator — matching how OvnRemoteEndpoints.ParseSslEndpoint expects it.
    private static string FormatForRemote(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();

    // The Geneve overlay tunnel endpoint (ovn-encap-ip). Host-local and authoritative: the operator's
    // agentsettings value when set, otherwise loopback (single-host only). Validation guarantees a set
    // value parses, but re-check defensively and fall back rather than throwing during chassis apply.
    private static async Task<IPAddress> ResolveEncapIp(Scope scope)
    {
        var configManager = scope.GetInstance<IVmHostAgentConfigurationManager>();
        var hostSettingsProvider = scope.GetInstance<IHostSettingsProvider>();
        var agentConfig = await hostSettingsProvider.GetHostSettings()
            .Bind(configManager.GetCurrentConfiguration)
            .Match(c => c, _ => (VmHostAgentConfiguration?)null);

        var configured = agentConfig?.Ovn?.OverlayTransportIp;
        return !string.IsNullOrWhiteSpace(configured) && IPAddress.TryParse(configured, out var ip)
            ? ip
            : IPAddress.Loopback;
    }

    internal static ChassisPlan BuildChassisPlan(NetworkProvidersConfiguration config) =>
        BuildChassisPlan(config, IPAddress.Loopback, null);

    internal static ChassisPlan BuildChassisPlan(
        NetworkProvidersConfiguration config,
        IPAddress encapIp,
        ChassisSouthbound? southbound)
    {
        var plan = new ChassisPlan(EryphConstants.Networking.LocalChassisName)
            .AddGeneveTunnelEndpoint(encapIp);

        // Remote southbound: point ovn-remote at the SSL endpoint (an already-resolved IP literal — OVS
        // on Windows cannot dial a host name for an active 'ssl:' stream) and configure the OVS SSL table
        // with the agent's enrolled certificate (ovn-controller reads its client certificate from that
        // table, not per-connection). The southbound server is authenticated by certificate chain, not
        // host name, so dialing by IP does not weaken authentication. Co-located leaves the default
        // local-pipe southbound connection untouched.
        if (southbound is not null)
            plan = plan.SetSwitchSsl(
                    southbound.Pem.PrivateKeyPem, southbound.Pem.CertificatePem, southbound.Pem.CaBundlePem)
                with
            {
                SouthboundDatabase = new OvsDbConnection(southbound.Address, southbound.Port, ssl: true),
            };

        return Optional(config.NetworkProviders).ToSeq()
            .Flatten()
            .Filter(p => p.Type is NetworkProviderType.Overlay or NetworkProviderType.NatOverlay)
            .Filter(p => !string.IsNullOrWhiteSpace(p.BridgeName))
            .Fold(plan, (p, provider) => p.AddBridgeMapping(provider.Name, provider.BridgeName!));
    }

    // The remote OVN southbound database the chassis dials: the resolved IP literal (OVS on Windows
    // cannot dial a host name), the port, and the agent's enrolled certificate as PEM (SetSwitchSsl
    // configures the OVS SSL table from PEM strings).
    internal sealed record ChassisSouthbound(string Address, int Port, ComponentCertificatePem Pem)
    {
        // Redact: the record carries private-key PEM, and the default record ToString would print it.
        public override string ToString() => $"ChassisSouthbound {{ Address = {Address}, Port = {Port} }}";
    }

    private async Task UpdateNetworkProviders()
    {
        var runtime = AgentRuntime.New(serviceProvider);

        var container = serviceProvider as Container
            ?? throw new InvalidOperationException("serviceProvider is not a SimpleInjector Container.");
        await using var scope = AsyncScopedLifestyle.BeginScope(container);

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
