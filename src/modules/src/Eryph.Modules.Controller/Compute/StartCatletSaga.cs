﻿using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class StartCatletSaga :
        OperationTaskWorkflowSaga<StartCatletCommand, StartCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<StartCatletVMCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;

        public StartCatletSaga(IWorkflow workflow,
            IVirtualMachineDataService vmDataService) : base(workflow)
        {
            _vmDataService = vmDataService;
        }

        protected override Task Initiated(StartCatletCommand message)
        {
            return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
                None: () => Fail($"The catlet {message.Resource.Id} does not exist.").ToUnit(),
                Some: s => StartNewTask(new StartCatletVMCommand { CatletId = message.Resource.Id, VMId = s.VMId }).AsTask().ToUnit());
        }

        public Task Handle(OperationTaskStatusEvent<StartCatletVMCommand> message)
        {
            return FailOrRun(message, Complete);
        }

        protected override void CorrelateMessages(ICorrelationConfig<StartCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<StartCatletVMCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        }
    }
}
