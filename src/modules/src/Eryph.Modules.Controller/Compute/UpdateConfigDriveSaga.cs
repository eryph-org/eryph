using System.Threading.Tasks;
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
    internal class UpdateConfigDriveSaga :
        OperationTaskWorkflowSaga<UpdateConfigDriveCommand, UpdateConfigDriveSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;
        public UpdateConfigDriveSaga(IWorkflow workflow, IVirtualMachineDataService vmDataService, IVirtualMachineMetadataService metadataService) 
            : base(workflow)
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
                        StartNewTask(new UpdateCatletConfigDriveCommand
                            {
                                
                                VMId = s.VMId,
                                CatletId = s.Id,
                                CatletName = s.Name,
                                MachineMetadata = metadata
                            }).AsTask().ToUnit()));
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateConfigDriveSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
        }




    }
}