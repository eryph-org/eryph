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
    internal class UpdateConfigDriveSaga :
        OperationTaskWorkflowSaga<UpdateConfigDriveCommand, UpdateConfigDriveSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;
        public UpdateConfigDriveSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IVirtualMachineDataService vmDataService, IVirtualMachineMetadataService metadataService, IMessageContext messageContext) 
            : base(bus, taskDispatcher, messageContext)
        {
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        protected override Task Initiated(UpdateConfigDriveCommand message)
        {
            return _vmDataService.GetVM(message.Resource.Id).MatchAsync(
                None: () => Fail().ToUnit(),
                Some: s => _metadataService.GetMetadata(s.MetadataId).Match(
                    None: () => Fail().ToUnit(),
                    Some: metadata =>
                        StartNewTask(new UpdateVirtualCatletConfigDriveCommand
                            {

                                VMId = s.VMId,
                                CatletId = s.Id,
                                MachineMetadata = metadata
                            }).ToUnit()));
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateConfigDriveSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        }




    }
}