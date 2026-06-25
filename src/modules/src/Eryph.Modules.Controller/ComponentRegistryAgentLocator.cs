using System;

namespace Eryph.Modules.Controller;

/// <summary>
/// Resolves the responsible storage-management host agent from
/// <see cref="IComponentRegistry"/>. For a single-host deployment it returns
/// the one registered host. VM placement is provided separately by the runtime
/// host via <see cref="IPlacementCalculator"/>.
/// </summary>
internal sealed class ComponentRegistryAgentLocator(IComponentRegistry componentRegistry)
    : IStorageManagementAgentLocator
{
    public string FindAgentForDataStore(string dataStore, string environment) =>
        SingleHostAgent().AgentName;

    public string FindAgentForGenePool() =>
        SingleHostAgent().AgentName;

    private HostAgentComponent SingleHostAgent() =>
        componentRegistry.GetHostAgents().HeadOrNone().IfNone(() =>
            throw new InvalidOperationException(
                "No host agent is registered; cannot resolve a responsible agent."));
}
