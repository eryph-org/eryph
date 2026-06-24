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
internal class OpenSshChannelSaga(
    IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<OpenSshChannelCommand, EryphSagaData<OpenSshChannelSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<OpenSshChannelVMCommand>>
{
    public Task Handle(OperationTaskStatusEvent<OpenSshChannelVMCommand> message) =>
        FailOrRun(message, (OpenSshChannelVMCommandResponse response) => Complete(response));

    protected override async Task Initiated(OpenSshChannelCommand message)
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

        await StartNewTask(new OpenSshChannelVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
            AccessKeyValues = message.AccessKeyValues,
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<OpenSshChannelSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<OpenSshChannelVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
