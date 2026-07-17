using System;

namespace Eryph.Modules.Controller;

/// <summary>
/// A host agent known to the controller: the agent name used for command routing
/// and placement, the site it is located in, and its OVN chassis identity.
/// </summary>
/// <param name="AgentName">Agent name (today the host's machine name).</param>
/// <param name="SiteId">The site this host is located in.</param>
/// <param name="ChassisName">OVN chassis name for this host.</param>
/// <param name="ChassisPriority">Gateway chassis priority for this host.</param>
public sealed record HostAgentComponent(
    string AgentName,
    Guid SiteId,
    string ChassisName,
    short ChassisPriority);
