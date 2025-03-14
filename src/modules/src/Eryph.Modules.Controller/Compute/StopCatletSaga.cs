using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class StopCatletSaga :
    OperationTaskWorkflowSaga<StopCatletCommand, StopCatletSagaData>,
    IHandleMessages<OperationTaskStatusEvent<StopVMCommand>>,
    IHandleMessages<OperationTaskStatusEvent<ShutdownVMCommand>>

{
    private readonly IVirtualMachineDataService _vmDataService;

    public StopCatletSaga(IWorkflow workflow,
        IVirtualMachineDataService vmDataService) : base(workflow)
    {
        _vmDataService = vmDataService;
    }

    protected override Task Initiated(StopCatletCommand message)
    {
        return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
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
                    StartNewTask(new StopVMCommand
                    {
                        CatletId = message.Resource.Id,
                        VMId = s.VMId,
                        StopProcess = true,
                    }).AsTask().ToUnit(),
                _ => Fail($"The stop mode {message.Mode} is not supported").ToUnit(),
            });
    }

    protected override void CorrelateMessages(ICorrelationConfig<StopCatletSagaData> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<StartCatletVMCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<StopVMCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ShutdownVMCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
    }

    public Task Handle(OperationTaskStatusEvent<StopVMCommand> message)
    {
        return FailOrRun(message, Complete);
    }

    public Task Handle(OperationTaskStatusEvent<ShutdownVMCommand> message)
    {
        return FailOrRun(message, Complete);
    }
}
