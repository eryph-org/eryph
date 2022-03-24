﻿using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
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
    internal class UpdateConfigDriveSaga :
        OperationTaskWorkflowSaga<UpdateConfigDriveCommand, UpdateConfigDriveSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;
        public UpdateConfigDriveSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IVirtualMachineDataService vmDataService, IVirtualMachineMetadataService metadataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
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
                        _taskDispatcher.StartNew(Data.OperationId,
                            new UpdateVirtualMachineConfigDriveCommand
                            {

                                VMId = s.VMId,
                                MachineId = s.Id,
                                MachineMetadata = metadata
                            }).ToUnit()));
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand> message)
        {
            return FailOrRun(message, () => Complete());

        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateConfigDriveSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand>>(m => m.OperationId, m => m.OperationId);
        }




    }
}