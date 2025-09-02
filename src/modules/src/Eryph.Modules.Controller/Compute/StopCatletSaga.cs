using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class StopCatletSaga(
    IWorkflow workflow,
    ICatletDataService vmDataService)
    : OperationTaskWorkflowSaga<StopCatletCommand, EryphSagaData<StopCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ShutdownVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<StopVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<KillVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletStateCommand>>
{
    protected override async Task Initiated(StopCatletCommand message)
    {
        Data.Data.CatletId = message.CatletId;
        var catlet = await vmDataService.Get(message.CatletId);
        if (catlet is null)
        {
            await Fail($"The catlet {message.CatletId} does not exist.");
            return;
        }
        Data.Data.VmId = catlet.VmId;

        switch (message.Mode)
        {
            case CatletStopMode.Shutdown:
                await StartNewTask(new ShutdownVMCommand
                {
                    CatletId = message.CatletId,
                    VmId = Data.Data.VmId,
                });
                return;
            case CatletStopMode.Hard:
                await StartNewTask(new StopVMCommand
                {
                    CatletId = message.CatletId,
                    VmId = Data.Data.VmId,
                });
                return;
            case CatletStopMode.Kill:
                await StartNewTask(new KillVMCommand
                {
                    CatletId = message.CatletId,
                    VmId = Data.Data.VmId,
                });
                return;
            default:
                await Fail($"The stop mode {message.Mode} is not supported");
                return;
        }
    }

    public Task Handle(OperationTaskStatusEvent<StopVMCommand> message) =>
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

    public Task Handle(OperationTaskStatusEvent<ShutdownVMCommand> message) =>
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

    public Task Handle(OperationTaskStatusEvent<KillVMCommand> message) =>
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
        FailOrRun(message, Complete);

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<StopCatletSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<StopVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ShutdownVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<KillVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletStateCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
