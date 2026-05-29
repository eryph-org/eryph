namespace Eryph.Messages.Components;

/// <summary>
/// A named, versioned cluster-configuration namespace owned by the controller and
/// distributed to entitled components. Host-local config (agent settings) and
/// identity clients are deliberately NOT domains — they are owned by their
/// components. New domains are added as later phases bring them under the
/// controller's authority.
/// </summary>
public enum ConfigDomain
{
    /// <summary>
    /// Controller-owned placement settings the agents need to participate in
    /// placement — the datastore and environment name catalog (the cluster
    /// vocabulary). Paths stay agent-local; capability matching and placement
    /// decisions are runtime, not distributed config.
    /// </summary>
    PlacementConfig,
}
