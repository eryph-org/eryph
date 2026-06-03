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
internal class AddSshKeySaga(IWorkflow workflow,
    ICatletDataService vmDataService) :
    OperationTaskWorkflowSaga<AddSshKeyCommand, EryphSagaData<AddSshKeySagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<AddSshKeyVMCommand>>
{
    protected override async Task Initiated(AddSshKeyCommand message)
    {
        Data.Data.SubjectId = message.SubjectId;
        var catlet = await vmDataService.Get(message.CatletId);
        if (catlet is null)
        {
            await Fail($"The catlet {message.CatletId} does not exist.");
            return;
        }

        Data.Data.VmId = catlet.VmId;

        await StartNewTask(new AddSshKeyVMCommand
        {
            CatletId = message.CatletId,
            VmId = Data.Data.VmId,
            SubjectId = message.SubjectId,
            PublicKey = message.PublicKey,
            KeyExpiry = message.KeyExpiry,
        });
    }

    public Task Handle(OperationTaskStatusEvent<AddSshKeyVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<AddSshKeySagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<AddSshKeyVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
