using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

[UsedImplicitly]
public class UpdateProjectNetworkPlanCommandHandler(
    ISysEnvironment sysEnvironment,
    IOVNSettings ovnSettings,
    ILogger logger,
    INetworkProviderManager networkProviderManager,
    IProjectNetworkPlanBuilder planBuilder,
    IStateStore stateStore,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<UpdateProjectNetworkPlanCommand>>
{
    public async Task Handle(OperationTask<UpdateProjectNetworkPlanCommand> message)
    {
        await messaging.ProgressMessage(message.OperationId, message.TaskId, "Rebuilding project network settings");
        
        await UpdateProjectNetwork(message.Command.ProjectId)
            .FailOrComplete(messaging, message);
    }

    private Aff<UpdateProjectNetworkPlanResponse> UpdateProjectNetwork(
        Guid projectId) =>
        from providerConfig in networkProviderManager.GetCurrentConfiguration().ToAff(e => e)
        from networkPlan in planBuilder.GenerateNetworkPlan(projectId, providerConfig).ToAff(e => e)
        from appliedPlan in use(
            Eff(() => new CancellationTokenSource(TimeSpan.FromMinutes(5))),
            cancelSource =>
                from _ in SuccessAff<Unit>(unit)
                let networkPlanRealizer = new NetworkPlanRealizer(
                    new OVNControlTool(sysEnvironment, ovnSettings.NorthDBConnection),
                    logger)
                from appliedPlan in networkPlanRealizer.ApplyNetworkPlan(networkPlan, cancelSource.Token).ToAff(e => e)
                select appliedPlan)
        let response = new UpdateProjectNetworkPlanResponse
        {
            ProjectId = projectId,
            UpdatedAddresses = appliedPlan.PlannedNATRules
                .Values.Map(port => new NetworkNeighborRecord
                {
                    IpAddress = port.ExternalIP,
                    MacAddress = port.ExternalMAC
                })
                .ToArray()
        }
        select response;
}