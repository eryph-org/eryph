using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Controller;

/// <summary>
/// Basic placement for the standalone controller: every catlet is placed on the first registered host
/// agent. <see cref="IComponentRegistry.GetHostAgents"/> returns agents in a deterministic order, so the
/// selection is stable across calls and consistent with the other registry consumers. This is
/// intentionally simple — no scoring across hosts and no architecture-capability filtering, because the
/// controller does not yet receive the hosts' reported capabilities (architecture, free resources)
/// through the component registry. Real multi-host scheduling is a later step; until then this places on
/// the single available agent, and a host that cannot run the requested architecture surfaces the
/// failure when the catlet is realized.
/// </summary>
internal sealed class ControllerPlacementCalculator(IComponentRegistry componentRegistry)
    : IPlacementCalculator
{
    public Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Architecture architecture) =>
        componentRegistry.GetHostAgents()
            .Map(agent => agent.AgentName)
            .HeadOrNone()
            .ToEither(() => Error.New("No host agent is registered; cannot place the catlet."));
}
