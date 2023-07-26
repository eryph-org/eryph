using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Operations;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class StartCatletSaga :
        OperationTaskWorkflowSaga<StartCatletCommand, StartCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<StartVirtualCatletCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;

        public StartCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IMessageContext messageContext,
            IVirtualMachineDataService vmDataService) : base(bus, taskDispatcher, messageContext)
        {
            _vmDataService = vmDataService;
        }

        protected override Task Initiated(StartCatletCommand message)
        {
            return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
                None: () => Fail().ToUnit(),
                Some: s => StartNewTask(new StartVirtualCatletCommand { CatletId = message.Resource.Id, VMId = s.VMId }).ToUnit());
        }

        public Task Handle(OperationTaskStatusEvent<StartVirtualCatletCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<StartCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<StartVirtualCatletCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        }




    }
}