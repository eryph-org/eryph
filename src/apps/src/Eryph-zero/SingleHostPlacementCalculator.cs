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
/// local host agent, but only if that host can run the requested architecture.
/// </summary>
internal sealed class SingleHostPlacementCalculator(
    IComponentRegistry componentRegistry,
    IHostArchitectureProvider hostArchitectureProvider)
    : IPlacementCalculator
{
    public Either<Error, string> CalculateVMPlacement(
        CatletConfig? dataConfig,
        Architecture architecture)
    {
        var hostArchitecture = hostArchitectureProvider.Architecture;
        if (!architecture.IsSatisfiedBy(hostArchitecture))
            return Error.New(
                $"The architecture '{architecture}' cannot be deployed on this host "
                + $"(host architecture '{hostArchitecture}').");

        return componentRegistry.GetHostAgents().HeadOrNone()
            .Map(agent => agent.AgentName)
            .ToEither(() => Error.New("No host agent is registered; cannot place the catlet."));
    }
}
