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
        ILoggerFactory loggerFactory)
        : BackgroundService
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<OvnRemoteEndpointService>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // The enrolled certificate is on disk by now: certificates-only enrollment blocks during
            // container configuration until it succeeds, which runs before any hosted service starts.
            var pem = certificateStore.ReadClientCertificatePem();
            if (pem is null)
            {
                _logger.LogError(
                    "The component certificate is not available; the OVN databases will not be exposed "
                    + "remotely. Check the component enrollment.");
                return;
            }

            // The database nodes start on their own threads; wait until both local sockets accept before
            // configuring the listeners (applying the plan talks to the databases over the local pipe).
            if (!await WaitForDatabase(ovnSettings.NorthDBConnection, "northbound", stoppingToken)
                || !await WaitForDatabase(ovnSettings.SouthDBConnection, "southbound", stoppingToken))
                return;

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
    }
}
