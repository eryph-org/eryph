using System;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Commands;
using Eryph.Messages.Resources.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class StartMachineSaga :
        OperationTaskWorkflowSaga<StartMachineCommand, StartMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<StartVMCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;

        public StartMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IVirtualMachineDataService vmDataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _vmDataService = vmDataService;
        }

        public override Task Initiated(StartMachineCommand message)
        {
            return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
                None: () => Fail().ToUnit(),
                Some: s => _taskDispatcher.StartNew(Data.OperationId,
                    new StartVMCommand { MachineId = message.Resource.Id, VMId = s.VMId }).ToUnit());
        }

        public Task Handle(OperationTaskStatusEvent<StartVMCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<StartMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<StartVMCommand>>(m => m.OperationId, m => m.OperationId);
        }




    }
}