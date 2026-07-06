using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Network;

/// <summary>
/// Realizes the controller-distributed <see cref="ConfigDomain.OvnCluster"/> topology — the OVN
/// gateway chassis groups — against the LOCAL northbound database. The network component hosts that
/// database and is the single writer of its cluster tables: it applies the chassis groups AND the
/// host-supplied northbound listeners (<see cref="IOvnNorthboundListener"/>) in one plan, via the
/// library's <see cref="ClusterPlanNorthboundRealizer"/>. Applying them together is what fixes the
/// clobber — the controller previously applied a chassis-only cluster plan as a remote client, whose
/// reconciliation removed the network's SSL listeners. eryph-zero contributes no listener, so there
/// the plan carries only chassis (its empty connection reconcile is a no-op on a local-pipe database).
/// </summary>
internal sealed class OvnClusterConfigRealizer(
    IEnumerable<IOvnNorthboundListener> northboundListeners,
    IOVNSettings ovnSettings,
    ISystemEnvironment systemEnvironment,
    ILogger<OvnClusterConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.OvnCluster;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<OvnClusterConfig>(payload)
                     ?? throw new InvalidOperationException(
                         "The OVN cluster configuration payload was empty.");

        // Guard against a malformed/version-skewed payload that deserialized with a null or empty group
        // name or a null chassis list — applying such a plan would create an unnamed group or throw
        // while iterating. Individual invalid/duplicate chassis entries are handled in BuildPlan.
        if (string.IsNullOrWhiteSpace(config.ChassisGroupName) || config.Chassis is null)
            throw new InvalidOperationException(
                "The OVN cluster configuration payload is invalid: a chassis group name and a chassis "
                + "list are required.");

        // Wait for the local northbound database to accept connections before applying. The database
        // node starts on its own thread, so a config snapshot can arrive before ovsdb-server is
        // listening on the local pipe. There is no automatic re-push of an unchanged payload (config
        // drift re-distribution is a separate concern), so failing here without waiting could leave the
        // chassis and listener unset for the process lifetime; waiting closes that startup race.
        await WaitForNorthboundDatabaseAsync(cancellationToken);

        var plan = BuildPlan(config, northboundListeners);

        var realizer = new ClusterPlanNorthboundRealizer(
            systemEnvironment,
            new OVNControlTool(systemEnvironment, ovnSettings.NorthDBConnection));

        await realizer.ApplyClusterPlan(plan, cancellationToken).Match(
            Right: _ => unit,
            Left: error => throw new InvalidOperationException(
                $"Failed to apply the OVN cluster configuration: {error.Message}"));

        logger.LogInformation(
            "Applied OVN cluster configuration v{Version} to the local northbound database "
            + "(chassis group '{ChassisGroup}', {ChassisCount} chassis).",
            version, config.ChassisGroupName, config.Chassis.Count);
    }

    /// <summary>
    /// Builds the northbound cluster plan: the gateway chassis group and its members plus the
    /// host-supplied listeners, all in one reconciled plan. Chassis are added at most once per name and
    /// nameless entries are skipped — the registry can currently yield the same chassis name for more
    /// than one agent, and <see cref="ClusterPlanConfigurationExtensions.AddChassis"/> throws on a
    /// duplicate key, which would otherwise fail the whole apply.
    /// </summary>
    internal static ClusterPlan BuildPlan(
        OvnClusterConfig config,
        IEnumerable<IOvnNorthboundListener> northboundListeners)
    {
        var plan = new ClusterPlan().AddChassisGroup(config.ChassisGroupName);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chassis in config.Chassis)
        {
            if (string.IsNullOrWhiteSpace(chassis?.Name) || !seen.Add(chassis.Name))
                continue;
            plan = plan.AddChassis(config.ChassisGroupName, chassis.Name, chassis.Priority);
        }

        // Fold in the host-supplied northbound listeners (none in eryph-zero, a pssl:6641 SSL listener in
        // the split runtime) so they are part of the same reconciled plan as the chassis.
        foreach (var listener in northboundListeners)
            plan = listener.Configure(plan);

        return plan;
    }

    private async Task WaitForNorthboundDatabaseAsync(CancellationToken cancellationToken)
    {
        var either = await ovnSettings.NorthDBConnection
            .WaitForDbSocket(systemEnvironment, cancellationToken);

        var started = either.Match(
            s => s,
            error => throw new InvalidOperationException(
                $"Failed to wait for the OVN northbound database: {error.Message}"));

        if (!started)
            throw new InvalidOperationException(
                "The OVN northbound database did not become available, so the OVN cluster configuration "
                + "cannot be applied.");
    }
}
