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
internal class RemoveSshKeySaga(IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<RemoveSshKeyCommand, EryphSagaData<RemoveSshKeySagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<RemoveSshKeyVMCommand>>
{
    protected override async Task Initiated(RemoveSshKeyCommand message)
    {
        Data.Data.SubjectId = message.SubjectId;
        var catlet = await vmDataService.Get(message.CatletId);
        if (catlet is null)
        {
            await Fail($"The catlet {message.CatletId} does not exist.");
            return;
        }

        Data.Data.VmId = catlet.VmId;

        await StartNewTask(new RemoveSshKeyVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
            SubjectId = message.SubjectId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveSshKeyVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<RemoveSshKeySagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<RemoveSshKeyVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
