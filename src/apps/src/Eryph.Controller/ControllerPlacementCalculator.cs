using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Controller;

/// <summary>
/// Basic placement for the standalone controller: every catlet is placed on the first registered host
/// agent in the site of its environment. <see cref="IComponentRegistry.GetHostAgents"/> returns agents
/// in a deterministic order, so the selection is stable across calls and consistent with the other
/// registry consumers. This is intentionally simple — no scoring across hosts and no
/// architecture-capability filtering, because the controller does not yet receive the hosts' reported
/// capabilities (architecture, free resources) through the component registry. Real multi-host
/// scheduling is a later step; until then this places on the first available agent in the site, and a
/// host that cannot run the requested architecture surfaces the failure when the catlet is realized.
/// </summary>
internal sealed class ControllerPlacementCalculator(IComponentRegistry componentRegistry)
    : IPlacementCalculator
{
    public Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Guid siteId,
        Architecture architecture) =>
        componentRegistry.GetHostAgents()
            .Filter(agent => agent.SiteId == siteId)
            .Map(agent => agent.AgentName)
            .HeadOrNone()
            // Name the site: placement is site scoped, so "no agent" is only actionable once the
            // operator knows which site was searched.
            .ToEither(() => Error.New(
                $"No host agent is registered in the site {siteId} of the catlet's environment; "
                + "cannot place the catlet."));
}
