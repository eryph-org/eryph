using System;
using System.Threading;
using Dbosoft.OVN;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public interface IProjectNetworkPlanBuilder
{
    EitherAsync<Error, NetworkPlan> GenerateNetworkPlan(
        Guid projectId,
        NetworkProvidersConfiguration providerConfig);
}
