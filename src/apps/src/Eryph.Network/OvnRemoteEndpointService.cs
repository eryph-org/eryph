using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Eryph.Core;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eryph.Network
{
    /// <summary>
    /// Opens the OVN northbound and southbound databases to remote clients over SSL once the local
    /// databases are up. The databases are hosted on the local pipe by the network module; this service
    /// applies a cluster plan that sets the SSL material (from the component's enrolled certificate) and
    /// adds passive SSL listeners (<c>pssl:6641</c> / <c>pssl:6642</c>) via the connections/ssl tables —
    /// the controller dials the northbound database and the agents' ovn-controller dials the southbound
    /// database, all authenticated with certificates from the single component CA.
    /// </summary>
    internal sealed class OvnRemoteEndpointService(
        IComponentCertificateStore certificateStore,
        IOVNSettings ovnSettings,
        ISystemEnvironment systemEnvironment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory)
        : BackgroundService
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<OvnRemoteEndpointService>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // The enrolled certificate is on disk by now: certificates-only enrollment blocks during
            // container configuration until it succeeds, which runs before any hosted service starts (and
            // this process requires mTLS, so the store is always present). If the PEM material is missing
            // anyway (e.g. a crash left only the PFX), fail fast rather than running on without listeners:
            // the module still advertises the SSL endpoints, so a silent "no listeners" state would leave
            // the controller/agents dialling a dead port.
            var pem = certificateStore.ReadClientCertificatePem();
            if (pem is null)
            {
                FailFast(
                    "The component certificate (PEM) is not available, so the OVN databases cannot be "
                    + "exposed over SSL. Stopping so the service manager restarts the process and re-runs "
                    + "enrollment; check the component enrollment if this persists.");
                return;
            }

            // The database nodes start on their own threads; wait until both local sockets accept before
            // configuring the listeners (applying the plan talks to the databases over the local pipe).
            // The databases are hosted by this process, so a database that never comes up is a broken
            // process: fail fast (unless we are shutting down) for the same reason as above.
            if (!await WaitForDatabase(ovnSettings.NorthDBConnection, "northbound", stoppingToken)
                || !await WaitForDatabase(ovnSettings.SouthDBConnection, "southbound", stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                    return;
                FailFast(
                    "An OVN database did not become available, so the SSL listeners cannot be opened. "
                    + "Stopping so the service manager restarts the process.");
                return;
            }

            // Listen on all interfaces (null address) so the controller and agents can dial from other
            // hosts; clients are authenticated by certificate against the component CA.
            // The controller consumes the northbound listener today. The southbound listener is opened and
            // advertised so the agents' ovn-controller can connect to it remotely; wiring the agents to
            // dial it (instead of the local pipe) is a separate slice and is not consumed yet.
            var clusterPlan = new ClusterPlan()
                .SetNorthboundSsl(pem.PrivateKeyPem, pem.CertificatePem, pem.CaBundlePem)
                .AddNorthboundConnection(OvnRemoteEndpoints.NorthboundPort, ssl: true)
                .SetSouthboundSsl(pem.PrivateKeyPem, pem.CertificatePem, pem.CaBundlePem)
                .AddSouthboundConnection(OvnRemoteEndpoints.SouthboundPort, ssl: true);

            var realizer = new ClusterPlanRealizer(
                systemEnvironment,
                new OVNControlTool(systemEnvironment, ovnSettings.NorthDBConnection),
                new OVNSouthboundControlTool(systemEnvironment, ovnSettings.SouthDBConnection));

            // Retry until the listeners are configured: a single transient failure must not leave the OVN
            // databases permanently unreachable over SSL. The databases are already up (waited on above),
            // so this only re-attempts the configuration, backing off between tries.
            for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
            {
                var result = await realizer.ApplyClusterPlan(clusterPlan, stoppingToken);
                var applied = result.Match(
                    Right: _ =>
                    {
                        _logger.LogInformation(
                            "OVN databases exposed over SSL: northbound pssl:{NbPort}, southbound pssl:{SbPort}.",
                            OvnRemoteEndpoints.NorthboundPort, OvnRemoteEndpoints.SouthboundPort);
                        return true;
                    },
                    Left: error =>
                    {
                        _logger.LogWarning(
                            "Failed to expose the OVN databases over SSL (attempt {Attempt}); retrying: {Error}",
                            attempt, error.Message);
                        return false;
                    });

                if (applied)
                    return;

                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task<bool> WaitForDatabase(
            OvsDbConnection connection, string name, CancellationToken cancellationToken)
        {
            var either = await connection.WaitForDbSocket(systemEnvironment, cancellationToken);
            return either.Match(
                Right: started =>
                {
                    if (!started)
                        _logger.LogError("Timed out waiting for the {Name} database to start.", name);
                    return started;
                },
                Left: error =>
                {
                    _logger.LogError(
                        "Failed to wait for the {Name} database: {Error}", name, error.Message);
                    return false;
                });
        }

        // Stop the whole host with a non-zero exit code so the service manager (Windows SCM / systemd)
        // restarts the process. This is deterministic regardless of the host's BackgroundService
        // exception behaviour: a half-configured network process that advertises SSL endpoints it never
        // opened is worse than a restart.
        private void FailFast(string message)
        {
            _logger.LogCritical(message);
            Environment.ExitCode = 1;
            applicationLifetime.StopApplication();
        }
    }
}
