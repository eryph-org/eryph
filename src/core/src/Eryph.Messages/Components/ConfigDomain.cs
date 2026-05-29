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
    /// <summary>Projects and their role assignments. Already DB-authoritative; the pilot domain.</summary>
    Projects,
}
