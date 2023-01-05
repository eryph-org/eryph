using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

namespace Eryph.Modules.Controller.Networks;

[UsedImplicitly]
public class UpdateProjectNetworkPlanCommandHandler : IHandleMessages<OperationTask<UpdateProjectNetworkPlanCommand>>
{
    private readonly ISysEnvironment _sysEnvironment;
    private readonly IOVNSettings _ovnSettings;
    private readonly ILogger _logger;
    private readonly IBus _bus;
    private readonly IProjectNetworkPlanBuilder _planBuilder;
    private readonly IStateStore _stateStore;


    public UpdateProjectNetworkPlanCommandHandler(
        ISysEnvironment sysEnvironment, 
        IOVNSettings ovnSettings, 
        ILogger logger, IBus bus, 
        IProjectNetworkPlanBuilder planBuilder, 
        IStateStore stateStore)
    {
        _sysEnvironment = sysEnvironment;
        _ovnSettings = ovnSettings;
        _logger = logger;
        _bus = bus;
        _planBuilder = planBuilder;
        _stateStore = stateStore;
    }

    public async Task Handle(OperationTask<UpdateProjectNetworkPlanCommand> message)
    {
        var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        await ProgressMessage(message.OperationId, message.TaskId, "Rebuilding project network settings");

        // build plan and save it to DB (to make sure that everything can be saved
        // we cannot use unit of work here, as OVN changes are not included in uow
        var generatedPlan = await (from plan in _planBuilder.GenerateNetworkPlan(message.Command.ProjectId, cancelSource.Token)
                let _ = _stateStore.SaveChangesAsync(cancelSource.Token)
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
            .Map(_ => new UpdateProjectNetworkPlanResponse
            {
                ProjectId = message.Command.ProjectId
            })
            .FailOrComplete(_bus, message);
    }

    private async Task ProgressMessage(Guid operationId, Guid taskId, string message)
    {
        using var scope = new RebusTransactionScope();
        await _bus.Publish(new OperationTaskProgressEvent
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            TaskId = taskId,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        // commit it like this
        await scope.CompleteAsync().ConfigureAwait(false);
    }
}