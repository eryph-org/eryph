using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class StartCatletSaga :
        OperationTaskWorkflowSaga<StartCatletCommand, StartCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<StartVirtualCatletCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;

        public StartCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IVirtualMachineDataService vmDataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _vmDataService = vmDataService;
        }

        protected override Task Initiated(StartCatletCommand message)
        {
            return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
                None: () => Fail().ToUnit(),
                Some: s => _taskDispatcher.StartNew(Data.OperationId,
                    new StartVirtualCatletCommand { CatletId = message.Resource.Id, VMId = s.VMId }).ToUnit());
        }

        public Task Handle(OperationTaskStatusEvent<StartVirtualCatletCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<StartCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<StartVirtualCatletCommand>>(m => m.OperationId, m => m.OperationId);
        }




    }
}