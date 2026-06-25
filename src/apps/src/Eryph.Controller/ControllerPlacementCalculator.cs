using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Controller;

/// <summary>
/// Placement for the standalone controller runtime.
/// </summary>
/// <remarks>
/// TODO: select the host agent and validate the requested architecture against the
/// capabilities the registered host agents report, instead of returning the first
/// registered agent. Until that exists this returns the single registered host.
/// </remarks>
internal sealed class ControllerPlacementCalculator(IComponentRegistry componentRegistry)
    : IPlacementCalculator
{
    public Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Architecture architecture) =>
        componentRegistry.GetHostAgents().HeadOrNone()
            .Map(agent => agent.AgentName)
            .ToEither(() => Error.New("No host agent is registered; cannot place the catlet."));
}
