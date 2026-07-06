using System.Collections.Generic;

namespace Eryph.Core.Network;

/// <summary>
/// The OVN northbound cluster topology the controller owns and distributes to the
/// network component (config domain <c>OvnCluster</c>). It carries the gateway
/// <see cref="ChassisGroupName"/> and its member chassis; the network component — which
/// hosts the northbound database — realizes this against its local database together with
/// the connection/SSL configuration it owns. The controller never writes the northbound
/// database itself, so the connection listeners it sets are not reconciled away.
/// </summary>
public sealed record OvnClusterConfig
{
    public required string ChassisGroupName { get; init; }

    public required IReadOnlyList<OvnClusterChassis> Chassis { get; init; }
}

/// <summary>
/// A single OVN chassis (a host agent running ovn-controller) and its gateway priority
/// within the <see cref="OvnClusterConfig.ChassisGroupName"/> group.
/// </summary>
public sealed record OvnClusterChassis
{
    public required string Name { get; init; }

    public required short Priority { get; init; }
}
