using System;
using System.Linq;
using System.Threading;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using LanguageExt;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller;

/// <summary>
/// Resolves the deployment's host agents from the durable <see cref="ComponentRegistration"/> catalog
/// (via <see cref="IComponentRegistryService"/>), so placement, storage-agent location and the OVN
/// cluster topology see the real registered agents in a split/cluster runtime — unlike
/// <see cref="SingleHostComponentRegistry"/>, which only ever reports the local machine. The standalone
/// controller host wires this; eryph-zero keeps the single-host implementation.
/// </summary>
public sealed class RegistryBackedComponentRegistry(Container container) : IComponentRegistry
{
    public Seq<HostAgentComponent> GetHostAgents()
    {
        // IComponentRegistryService reads the state DB and is scoped, but this seam is a singleton
        // resolved by singleton consumers — so open a dedicated scope per call (the OvnNorthbound
        // provider does the same). The consumers (placement, storage-agent location, OVN topology) are
        // synchronous, so block on the async read: these run on Rebus/Quartz worker threads with no
        // synchronization context, so there is no deadlock.
        using var scope = AsyncScopedLifestyle.BeginScope(container);
        var registry = scope.GetInstance<IComponentRegistryService>();
        var active = registry.GetActiveAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Order deterministically by agent name so every consumer of this seam (placement,
        // storage-agent location, OVN topology) picks the same agent from the same catalog — the
        // state DB returns no ordering guarantee, so without this a catlet's VM could be placed on
        // one host while its disks are resolved to another.
        return active.ToSeq()
            .Filter(c => c.ComponentType == ComponentType.VMHostAgent)
            .Choose(ToHostAgent)
            .OrderBy(agent => agent.AgentName, StringComparer.OrdinalIgnoreCase)
            .ToSeq();
    }

    /// <summary>
    /// Maps a host-agent registration to the routing/placement view. The agent name is the queue-name
    /// suffix the agent chose (its <see cref="Environment.MachineName"/>), i.e. the segment after the
    /// <c>eryph.vmhostagent.</c> prefix in its inbound queue — NOT <see cref="ComponentRegistration.MachineName"/>,
    /// which is the FQDN and differs in case and domain from the queue key. Returns <c>None</c> for a row
    /// whose inbound queue does not match the expected shape, so a malformed row is skipped rather than
    /// producing a command routed to a queue that does not exist.
    /// </summary>
    internal static Option<HostAgentComponent> ToHostAgent(ComponentRegistration registration)
    {
        const string prefix = QueueNames.VMHostAgent + ".";
        if (registration.InboundQueue is null
            || !registration.InboundQueue.StartsWith(prefix, StringComparison.Ordinal))
            return None;

        var agentName = registration.InboundQueue[prefix.Length..];
        if (string.IsNullOrWhiteSpace(agentName))
            return None;

        // The agent registers its own OVN chassis under the well-known local chassis name; priority 1 as
        // the single-host provider used. (Distinct per-host chassis naming/priority for multi-host gateway
        // election is a separate concern — every agent registers its chassis as "local" today.)
        return Some(new HostAgentComponent(
            agentName, registration.SiteId, EryphConstants.Networking.LocalChassisName, 1));
    }
}
