using System.Text.Json.Serialization;

namespace Eryph.Messages.Components;

/// <summary>
/// A named, versioned cluster-configuration namespace owned by the controller and
/// distributed to entitled components. Host-local config (agent settings) and
/// identity clients are deliberately NOT domains — they are owned by their
/// components. New domains are added as later phases bring them under the
/// controller's authority.
/// </summary>
/// <remarks>
/// Serialized by name (not ordinal) on the wire and in the
/// <c>ConfigRecord</c>/<c>ComponentRegistration</c> payloads, so storage stays
/// readable and is not invalidated by reordering the enum.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigDomain
{
    /// <summary>
    /// Controller-owned placement settings the agents need to participate in
    /// placement — the datastore name catalog (the cluster vocabulary) and the
    /// storage each environment maps to. Paths stay agent-local; capability
    /// matching and placement decisions are runtime, not distributed config.
    /// The environments themselves are defined by <see cref="Environments"/>.
    /// </summary>
    StorageConfig,

    /// <summary>
    /// Controller-owned environment catalog: the environment names (the cluster
    /// vocabulary) and the site which realizes each of them. Distributed to the
    /// agents so they can reject placement on an environment the controller does
    /// not know, and read by the controller to resolve the site a new resource is
    /// pinned to. Which storage an environment maps to is separate
    /// (<see cref="StorageConfig"/>), because paths are agent-local while the
    /// definition is global.
    /// </summary>
    Environments,

    /// <summary>
    /// Controller-owned network-provider configuration (the provider/bridge/subnet/
    /// IP-pool definitions, today the host-local <c>p_networks.yml</c>). The controller
    /// is the authority; entitled agents persist the distributed copy to their local
    /// network provider settings so a host's networking can be rebuilt from it.
    /// </summary>
    NetworkProviders,

    /// <summary>
    /// The deployment's service endpoints (identity, compute API, base) so a component
    /// can reach the others without eryph-zero's in-process endpoint resolver. The
    /// controller is the single distribution authority; the canonical value per logical
    /// endpoint is the operator override when set, otherwise the address advertised by
    /// the component that hosts it on registration. Both sources are aggregated: the
    /// controller starts from the advertised endpoints and overlays operator overrides.
    /// </summary>
    Endpoints,

    /// <summary>
    /// The OVN northbound cluster topology (the gateway chassis group and its members).
    /// The controller computes it from the registered host agents; the network component,
    /// which hosts the northbound database, realizes it against its local database together
    /// with the connection/SSL configuration it owns. This keeps a single writer of the
    /// northbound connection tables, so the controller's client-mode plan application no
    /// longer reconciles away the network component's SSL listeners.
    /// </summary>
    OvnCluster,
}
