using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class StopCatletSaga(
    IWorkflow workflow,
    IVirtualMachineDataService vmDataService)
    : OperationTaskWorkflowSaga<StopCatletCommand, StopCatletSagaData>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ShutdownVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<StopVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<KillVMCommand>>
{
    protected override Task Initiated(StopCatletCommand message)
    {
        return vmDataService.GetVM(message.Resource.Id).MatchAsync(
            None: () => Fail($"The catlet {message.Resource.Id} does not exist.").ToUnit(),
            Some: s => message.Mode switch
            {
                CatletStopMode.Shutdown => 
                    StartNewTask(new ShutdownVMCommand
                    {
                        CatletId = message.Resource.Id,
                        VMId = s.VMId,
                    }).AsTask().ToUnit(),
                CatletStopMode.Hard =>
                    StartNewTask(new StopVMCommand
                    {
                        CatletId = message.Resource.Id,
                        VMId = s.VMId,
                    }).AsTask().ToUnit(),
                CatletStopMode.Kill =>
                    StartNewTask(new KillVMCommand
                    {
                        CatletId = message.Resource.Id,
                        VMId = s.VMId,
                    }).AsTask().ToUnit(),
                _ => Fail($"The stop mode {message.Mode} is not supported").ToUnit(),
            });
    }

    public Task Handle(OperationTaskStatusEvent<StopVMCommand> message) =>
        FailOrRun(message, () => Complete());

    public Task Handle(OperationTaskStatusEvent<ShutdownVMCommand> message) =>
        FailOrRun(message, () => Complete());

    public Task Handle(OperationTaskStatusEvent<KillVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<StopCatletSagaData> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<StopVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ShutdownVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<KillVMCommand>>(
            m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
