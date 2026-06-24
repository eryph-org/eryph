using System.Collections.Generic;
using Eryph.Core;
using Eryph.ModuleCore.Components;

namespace Eryph.Network;

/// <summary>
/// Advertises the OVN northbound/southbound databases' remote SSL endpoints for the standalone
/// network host, so the controller (when it runs elsewhere) and the agents' ovn-controller can dial
/// them. Only the standalone host wires this; in-process under eryph-zero no provider is registered,
/// so nothing is advertised and clients reach the databases over the local pipe.
/// </summary>
internal sealed class OvnRemoteEndpointProvider(string advertisedHost) : IComponentEndpointProvider
{
    public IReadOnlyDictionary<string, string> GetAdvertisedEndpoints() =>
        new Dictionary<string, string>
        {
            [OvnRemoteEndpoints.NorthboundName] = $"ssl:{advertisedHost}:{OvnRemoteEndpoints.NorthboundPort}",
            [OvnRemoteEndpoints.SouthboundName] = $"ssl:{advertisedHost}:{OvnRemoteEndpoints.SouthboundPort}",
        };
}
