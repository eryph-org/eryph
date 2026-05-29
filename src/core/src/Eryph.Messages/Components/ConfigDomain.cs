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
    // No cluster-config domains are distributed yet. Domains are added as they are
    // implemented; the first planned downward domain is the controller-owned
    // datastore/environment name catalog. (Projects is deliberately not a domain:
    // its only consumer is networking, which is co-hosted with the controller.)
}
