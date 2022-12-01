using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class UpdateMachineSaga : OperationTaskWorkflowSaga<UpdateMachineCommand, UpdateMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateMachineConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateMachineNetworksCommand>>

    {
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;


        public UpdateMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService,
            IVirtualMachineMetadataService metadataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand> message)
        {

            return FailOrRun(message, () => Complete());
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineCommand> message)
        {
            if (Data.Updated)
                return Task.CompletedTask;

            return FailOrRun<UpdateVirtualMachineCommand, ConvergeVirtualMachineResult>(message, async r =>
            {
                Data.Updated = true;

                await _metadataService.SaveMetadata(r.MachineMetadata);

                await Bus.Send(new UpdateInventoryCommand
                {
                    AgentName = Data.AgentName,
                    Inventory = new List<VirtualMachineData> {r.Inventory}
                });

                await _vmDataService.GetVM(Data.MachineId).Match(
                    Some: data =>
                    {
                        return _taskDispatcher.StartNew(Data.OperationId, new UpdateVirtualMachineConfigDriveCommand
                        {
                            VMId = r.Inventory.VMId,
                            MachineId = Data.MachineId,
                            MachineMetadata = r.MachineMetadata,
                        });
                    },
                    None: () => Fail(new ErrorData
                        { ErrorMessage = $"Could not find virtual machine with machine id {Data.MachineId}" })
                );

            });
        }

        public Task Handle(OperationTaskStatusEvent<ValidateMachineConfigCommand> message)
        {
            if (Data.Validated)
                return Task.CompletedTask;

            return FailOrRun<ValidateMachineConfigCommand, ValidateMachineConfigCommand>(message, async r =>
            {
                Data.Config = r.Config;

                var machineInfo = await _vmDataService.GetVM(Data.MachineId);
                var projectId = machineInfo.Map(x => x.ProjectId).IfNone(Guid.Empty);

                await _taskDispatcher.StartNew(Data.OperationId, new UpdateMachineNetworksCommand
                {
                    MachineId = Data.MachineId,
                    Config = Data.Config,
                    ProjectId = projectId,
            });
            });
        }


        public Task Handle(OperationTaskStatusEvent<UpdateMachineNetworksCommand> message)
        {

            return FailOrRun<UpdateMachineNetworksCommand, UpdateMachineNetworksCommandResponse>(message, 
                async r =>
            {

                var optionalMachineData = await(
                    from vm in _vmDataService.GetVM(Data.MachineId)
                    from metadata in _metadataService.GetMetadata(vm.MetadataId)
                    select (vm, metadata));


                await optionalMachineData.Match(
                    Some: data =>
                    {
                        Data.Validated = true;
                        var (vm, metadata) = data;

                        return _taskDispatcher.StartNew(Data.OperationId, new UpdateVirtualMachineCommand
                        {
                            VMId = vm.VMId,
                            Config = Data.Config,
                            AgentName = Data.AgentName,
                            NewStorageId = _idGenerator.GenerateId(),
                            MachineMetadata = metadata,
                            MachineNetworkSettings = r.NetworkSettings
                        });
                    },
                    None: () => Fail(new ErrorData
                    { ErrorMessage = $"Could not find virtual machine with machine id {Data.MachineId}" })
                );

            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<ValidateMachineConfigCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineConfigDriveCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateMachineNetworksCommand>>(m => m.OperationId,
                d => d.OperationId);

        }

        protected override Task Initiated(UpdateMachineCommand message)
        {
            Data.Config = message.Config;
            Data.MachineId = message.Resource.Id;
            Data.AgentName = message.AgentName;


            return _taskDispatcher.StartNew(Data.OperationId, 
                new ValidateMachineConfigCommand
                {
                    MachineId = message.Resource.Id,
                    Config = message.Config,
                }
            );
        }


    }
}