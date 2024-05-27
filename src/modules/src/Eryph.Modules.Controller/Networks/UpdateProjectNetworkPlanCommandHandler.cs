using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Networks;

[UsedImplicitly]
public class UpdateProjectNetworkPlanCommandHandler : IHandleMessages<OperationTask<UpdateProjectNetworkPlanCommand>>
{
    private readonly ISysEnvironment _sysEnvironment;
    private readonly IOVNSettings _ovnSettings;
    private readonly ILogger _logger;
    private readonly ITaskMessaging _messaging;
    private readonly IProjectNetworkPlanBuilder _planBuilder;
    private readonly IStateStore _stateStore;


    public UpdateProjectNetworkPlanCommandHandler(
        ISysEnvironment sysEnvironment, 
        IOVNSettings ovnSettings, 
        ILogger logger,
        IProjectNetworkPlanBuilder planBuilder, 
        IStateStore stateStore, ITaskMessaging messaging)
    {
        _sysEnvironment = sysEnvironment;
        _ovnSettings = ovnSettings;
        _logger = logger;
        _planBuilder = planBuilder;
        _stateStore = stateStore;
        _messaging = messaging;
    }

    public async Task Handle(OperationTask<UpdateProjectNetworkPlanCommand> message)
    {
        var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        await _messaging.ProgressMessage(message.OperationId, message.TaskId, "Rebuilding project network settings");

        // build plan and save it to DB (to make sure that everything can be saved
        // we cannot use unit of work here, as OVN changes are not included in uow
        var generatedPlan = await (from plan in _planBuilder.GenerateNetworkPlan(message.Command.ProjectId, cancelSource.Token)
                from _ in Prelude.TryAsync(() =>_stateStore.SaveChangesAsync(cancelSource.Token))
                    .ToEither()
                select plan

            ).Match(
                plan => plan, 
                l =>
            {
                l.Throw();
                return new NetworkPlan(""); // will never be reached...
            });

        await UpdateOVN(generatedPlan, message);
    }

    public async Task UpdateOVN(NetworkPlan networkPlan, OperationTask<UpdateProjectNetworkPlanCommand> message)
    {
        var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var networkPlanRealizer =
            new NetworkPlanRealizer(new OVNControlTool(_sysEnvironment, _ovnSettings.NorthDBConnection), _logger);

        await networkPlanRealizer.ApplyNetworkPlan(networkPlan, cancelSource.Token)
            .Map(plan => new UpdateProjectNetworkPlanResponse
            {
                ProjectId = message.Command.ProjectId,
                UpdatedAddresses = plan.PlannedNATRules
                    .Values.Map(port => new NetworkNeighborRecord
                    {
                        IpAddress = port.ExternalIP,
                        MacAddress = port.ExternalMAC
                    })
                    .ToArray()
            })
            .FailOrComplete(_messaging, message);
    }

}