using System;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

namespace Eryph.Modules.Controller;

/// <summary>
/// Resolves the responsible host agent from <see cref="IComponentRegistry"/>.
/// Replaces the host-provided single-machine locator; for a single-host
/// deployment it returns the one registered host, preserving prior behavior.
/// Cluster-aware placement (using <see cref="CatletConfig"/> and host capabilities)
/// arrives in a later phase.
/// </summary>
internal sealed class ComponentRegistryAgentLocator(IComponentRegistry componentRegistry)
    : IPlacementCalculator, IStorageManagementAgentLocator
{
    public string CalculateVMPlacement(CatletConfig? dataConfig) =>
        SingleHostAgent().AgentName;

    public string FindAgentForDataStore(string dataStore, string environment) =>
        SingleHostAgent().AgentName;

    public string FindAgentForGenePool() =>
        SingleHostAgent().AgentName;

    private HostAgentComponent SingleHostAgent() =>
        componentRegistry.GetHostAgents().HeadOrNone().IfNone(() =>
            throw new InvalidOperationException(
                "No host agent is registered; cannot resolve a responsible agent."));
}
