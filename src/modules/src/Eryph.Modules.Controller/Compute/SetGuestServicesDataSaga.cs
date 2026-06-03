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
internal class SetGuestServicesDataSaga(IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<SetGuestServicesDataCommand, EryphSagaData<SetGuestServicesDataSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<SetGuestServicesDataVMCommand>>
{
    protected override async Task Initiated(SetGuestServicesDataCommand message)
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

        await StartNewTask(new SetGuestServicesDataVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
            Values = message.Values,
            RemoveKeys = message.RemoveKeys,
        });
    }

    public Task Handle(OperationTaskStatusEvent<SetGuestServicesDataVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<SetGuestServicesDataSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<SetGuestServicesDataVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
