using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Eryph.Core;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Network;

/// <summary>
/// Opens the OVN southbound database to remote clients over SSL once it is up. The database is hosted
/// on the local pipe by the network module; this service adds a passive SSL listener
/// (<c>pssl:6642</c>) via the connections/ssl tables, so the agents' ovn-controller can dial the
/// southbound database over SSL, authenticated by certificates from the single component CA.
/// </summary>
/// <remarks>
/// The northbound listener is NOT set here: the network module is the single writer of the northbound
/// cluster tables and applies its listener together with the gateway chassis in
/// <c>OvnClusterConfigRealizer</c> (fed by <see cref="ComponentCertificateNorthboundListener"/>), so a
/// chassis reconcile cannot remove it. This service owns only the southbound side, which is unrelated
/// to the chassis and must be reachable for agents independently of the controller's config.
/// </remarks>
internal sealed class OvnSouthboundEndpointService(
    IComponentCertificateStore certificateStore,
    IOVNSettings ovnSettings,
    ISystemEnvironment systemEnvironment,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory)
    : BackgroundService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OvnSouthboundEndpointService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The enrolled certificate is on disk by now (certificates-only enrollment blocks during
        // container configuration, before any hosted service starts). If the PEM material is missing
        // anyway, fail fast rather than run on without a listener: the module still advertises the SSL
        // endpoint, so a silent "no listener" state would leave the agents dialling a dead port.
        // This is a TLS *server* listener, so present the server certificate (serverAuth EKU).
        var pem = certificateStore.ReadServerCertificatePem();
        if (pem is null)
        {
            FailFast(
                "The component server certificate (PEM) is not available, so the OVN southbound database "
                + "cannot be exposed over SSL. Stopping so the service manager restarts the process and "
                + "re-runs enrollment; check the component enrollment if this persists.");
            return;
        }

        // The database node starts on its own thread; wait until the local socket accepts before
        // configuring the listener (applying the plan talks to the database over the local pipe).
        if (!await WaitForDatabase(ovnSettings.SouthDBConnection, "southbound", stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested)
                return;
            FailFast(
                "The OVN southbound database did not become available, so the SSL listener cannot be "
                + "opened. Stopping so the service manager restarts the process.");
            return;
        }

        // Listen on all interfaces (null address) so the agents can dial from other hosts; clients are
        // authenticated by certificate against the component CA.
        var clusterPlan = new ClusterPlan()
            .SetSouthboundSsl(pem.PrivateKeyPem, pem.CertificatePem, pem.CaBundlePem)
            .AddSouthboundConnection(OvnRemoteEndpoints.SouthboundPort, true);

        var realizer = new ClusterPlanSouthboundRealizer(
            systemEnvironment,
            new OVNSouthboundControlTool(systemEnvironment, ovnSettings.SouthDBConnection));

        // Retry until the listener is configured: a single transient failure must not leave the OVN
        // southbound database permanently unreachable over SSL. The database is already up (waited on
        // above), so this only re-attempts the configuration, backing off between tries.
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            var result = await realizer.ApplyClusterPlan(clusterPlan, stoppingToken);
            var applied = result.Match(
                _ =>
                {
                    _logger.LogInformation(
                        "OVN southbound database exposed over SSL: pssl:{SbPort}.",
                        OvnRemoteEndpoints.SouthboundPort);
                    return true;
                },
                error =>
                {
                    _logger.LogWarning(
                        "Failed to expose the OVN southbound database over SSL (attempt {Attempt}); "
                        + "retrying: {Error}",
                        attempt, error.Message);
                    return false;
                });

            if (applied)
                return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<bool> WaitForDatabase(
        OvsDbConnection connection, string name, CancellationToken cancellationToken)
    {
        var either = await connection.WaitForDbSocket(systemEnvironment, cancellationToken);
        return either.Match(
            started =>
            {
                if (!started)
                    _logger.LogError("Timed out waiting for the {Name} database to start.", name);
                return started;
            },
            error =>
            {
                _logger.LogError(
                    "Failed to wait for the {Name} database: {Error}", name, error.Message);
                return false;
            });
    }

    // Stop the whole host with a non-zero exit code so the service manager (Windows SCM / systemd)
    // restarts the process. A half-configured network process that advertises an SSL endpoint it never
    // opened is worse than a restart.
    private void FailFast(string message)
    {
        _logger.LogCritical(message);
        Environment.ExitCode = 1;
        applicationLifetime.StopApplication();
    }
}
