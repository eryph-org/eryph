using System;
using System.Threading;
using Dbosoft.OVN;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public interface IProjectNetworkPlanBuilder
{
    EitherAsync<Error, NetworkPlan> GenerateNetworkPlan(Guid projectId, CancellationToken cancellationToken);
}