using LanguageExt;

namespace Eryph.Modules.Controller;

/// <summary>
/// Read-side view of the components that make up the running eryph deployment.
/// In eryph-zero this is a single local host; a cluster runtime will back it with
/// the component-registration state in the state database. Placement, storage-agent
/// location and the OVN cluster topology resolve their answers from this seam
/// instead of hard-coding the local machine, so they transparently see real
/// registered components once that backing exists.
/// </summary>
public interface IComponentRegistry
{
    /// <summary>
    /// The host agents currently part of the deployment. The single-host
    /// implementation returns exactly one entry representing the local host.
    /// </summary>
    Seq<HostAgentComponent> GetHostAgents();
}
