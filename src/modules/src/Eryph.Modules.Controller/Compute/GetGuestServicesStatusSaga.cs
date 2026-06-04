using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class GetGuestServicesStatusSaga(IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<GetGuestServicesStatusCommand, EryphSagaData<GetGuestServicesStatusSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<GetGuestServicesStatusVMCommand>>
{
    protected override async Task Initiated(GetGuestServicesStatusCommand message)
    {
        var catlet = await vmDataService.Get(message.CatletId);
        if (catlet is null)
        {
            await Fail($"The catlet {message.CatletId} does not exist.");
            return;
        }

        Data.Data.VmId = catlet.VmId;
        if (Data.Data.VmId == Guid.Empty)
        {
            await Fail("The catlet has not been provisioned yet.");
            return;
        }

        await StartNewTask(new GetGuestServicesStatusVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<GetGuestServicesStatusVMCommand> message) =>
        FailOrRun(message, (GetGuestServicesStatusVMCommandResponse response) => Complete(response));

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<GetGuestServicesStatusSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<GetGuestServicesStatusVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
