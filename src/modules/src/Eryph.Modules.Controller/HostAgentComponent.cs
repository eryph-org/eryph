namespace Eryph.Modules.Controller;

/// <summary>
/// A host agent known to the controller: the agent name used for command routing
/// and placement, together with its OVN chassis identity.
/// </summary>
/// <param name="AgentName">Agent name (today the host's machine name).</param>
/// <param name="ChassisName">OVN chassis name for this host.</param>
/// <param name="ChassisPriority">Gateway chassis priority for this host.</param>
public sealed record HostAgentComponent(
    string AgentName,
    string ChassisName,
    short ChassisPriority);
