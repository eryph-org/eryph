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
internal class GetProvisioningLogSaga(
    IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<GetProvisioningLogCommand, EryphSagaData<GetProvisioningLogSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<GetProvisioningLogVMCommand>>
{
    public Task Handle(OperationTaskStatusEvent<GetProvisioningLogVMCommand> message) =>
        FailOrRun(message, (GetProvisioningLogVMCommandResponse response) => Complete(response));

    protected override async Task Initiated(GetProvisioningLogCommand message)
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

        await StartNewTask(new GetProvisioningLogVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<GetProvisioningLogSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<GetProvisioningLogVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
