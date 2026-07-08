using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Networks;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Serves the <see cref="ConfigDomain.OvnCluster"/> payload — the OVN gateway chassis
/// topology the controller computes from the registered host agents
/// (<see cref="IClusterTopologyProvider"/>). The network component realizes it against its
/// local northbound database, so the controller no longer applies a cluster plan itself
/// (which would reconcile away the network component's connection/SSL listeners).
/// </summary>
internal sealed class OvnClusterConfigSource(
    IClusterTopologyProvider clusterTopologyProvider)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.OvnCluster;

    public Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken)
    {
        var config = new OvnClusterConfig
        {
            ChassisGroupName = clusterTopologyProvider.ChassisGroupName,
            Chassis = clusterTopologyProvider.GetChassis()
                .Map(c => new OvnClusterChassis { Name = c.ChassisName, Priority = c.Priority })
                .ToList(),
        };

        return Task.FromResult(JsonSerializer.Serialize(config));
    }
}
