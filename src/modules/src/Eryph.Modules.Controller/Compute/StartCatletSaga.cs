using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class StartCatletSaga(IWorkflow workflow,
    IVirtualMachineDataService vmDataService) :
    OperationTaskWorkflowSaga<StartCatletCommand, EryphSagaData<StartCatletSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<StartCatletVMCommand>>,
    IHandleMessages<OperationTaskStatusEvent<UpdateCatletStateCommand>>
{
    protected override async Task Initiated(StartCatletCommand message)
    {
        Data.Data.CatletId = message.CatletId;
        var catlet = await vmDataService.GetVM(message.CatletId);
        if (catlet.IsNone)
        {
            await Fail($"The catlet {message.CatletId} does not exist.");
            return;
        }
        Data.Data.VmId = catlet.ValueUnsafe().VMId;
        
        await StartNewTask(new StartCatletVMCommand
        {
            CatletId = message.CatletId,
            VMId = Data.Data.VmId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<StartCatletVMCommand> message) =>
        FailOrRun(message, async (CatletStateResponse response) =>
        {
            await StartNewTask(new UpdateCatletStateCommand
            {
                CatletId = Data.Data.CatletId,
                VmId = Data.Data.VmId,
                Status = response.Status,
                UpTime = response.UpTime,
                Timestamp = response.Timestamp,
            });
        });

    public Task Handle(OperationTaskStatusEvent<UpdateCatletStateCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<StartCatletSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<StartCatletVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletStateCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
