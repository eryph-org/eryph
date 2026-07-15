using System;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller;

/// <summary>
/// Resolves the responsible storage-management host agent from
/// <see cref="IComponentRegistry"/>: the first registered host in the site,
/// which for a single-host deployment is the one registered host. VM placement
/// is provided separately by the runtime host via <see cref="IPlacementCalculator"/>.
/// </summary>
internal sealed class ComponentRegistryAgentLocator(IComponentRegistry componentRegistry)
    : IStorageManagementAgentLocator
{
    public Either<Error, string> FindAgentForDataStore(string dataStore, Guid siteId) =>
        componentRegistry.GetHostAgents()
            .Filter(agent => agent.SiteId == siteId)
            .Map(agent => agent.AgentName)
            .HeadOrNone()
            .ToEither(() => Error.New(
                $"No host agent is registered in the site of the data store '{dataStore}'; "
                + "cannot resolve a responsible agent."));

    public string FindAgentForGenePool() =>
        // The gene pool is not site bound: it stores genes, which are not resources of a project.
        componentRegistry.GetHostAgents().HeadOrNone().Map(agent => agent.AgentName).IfNone(() =>
            throw new InvalidOperationException(
                "No host agent is registered; cannot resolve a responsible agent."));
}
