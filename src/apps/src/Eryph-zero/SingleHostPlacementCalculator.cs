using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller;
using Eryph.Modules.HostAgent.Inventory;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

/// <summary>
/// Placement for the embedded single-host runtime: the catlet always runs on the
/// local host agent, but only if that host can run the requested architecture and
/// is in the site of the catlet's environment. Both hold for every catlet as long
/// as there is one site, but the checks are the real ones, not a stub.
/// </summary>
internal sealed class SingleHostPlacementCalculator(
    IComponentRegistry componentRegistry,
    IHostArchitectureProvider hostArchitectureProvider)
    : IPlacementCalculator
{
    public Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Guid siteId,
        Architecture architecture)
    {
        // The site is the harder constraint: a host elsewhere could never run this catlet, whereas a
        // host in the site merely has to satisfy the architecture. Checking it first keeps the two
        // failures distinguishable instead of reporting an architecture problem for a locality one.
        var agentsInSite = componentRegistry.GetHostAgents()
            .Filter(agent => agent.SiteId == siteId);
        if (agentsInSite.IsEmpty)
            return Error.New(
                "No host agent is registered in the site of the catlet's environment; "
                + "cannot place the catlet.");

        var hostArchitecture = hostArchitectureProvider.Architecture;
        if (!architecture.IsSatisfiedBy(hostArchitecture))
            return Error.New(
                $"The architecture '{architecture}' cannot be deployed on this host "
                + $"(host architecture '{hostArchitecture}').");

        return agentsInSite.HeadOrNone()
            .Map(agent => agent.AgentName)
            // The agents were filtered by site, so an empty set does not mean none is registered —
            // it means none is in this one, which is what the operator has to act on.
            .ToEither(() => Error.New(
                $"No host agent is registered in the site {siteId} of the catlet's environment; "
                + "cannot place the catlet."));
    }
}
