using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class UpdateCatletSaga : OperationTaskWorkflowSaga<UpdateCatletCommand, UpdateCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

    {
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;


        public UpdateCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService,
            IVirtualMachineMetadataService metadataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.OperationId,
                d => d.OperationId);

        }

        protected override Task Initiated(UpdateCatletCommand message)
        {
            Data.Config = message.Config;
            Data.CatletId = message.Resource.Id;
            Data.AgentName = message.AgentName;


            return _taskDispatcher.StartNew(Data.OperationId,
                new ValidateCatletConfigCommand
                {
                    MachineId = message.Resource.Id,
                    Config = message.Config,
                }
            );
        }

        public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
        {
            if (Data.Validated)
                return Task.CompletedTask;

            return FailOrRun<ValidateCatletConfigCommand, ValidateCatletConfigCommand>(message, async r =>
            {
                Data.Config = r.Config;
                Data.Validated = true;
                var machineInfo = await _vmDataService.GetVM(Data.CatletId);
                Data.ProjectId = machineInfo.Map(x => x.ProjectId).IfNone(Guid.Empty);
                Data.AgentName = machineInfo.Map(x => x.AgentName).IfNone("");

                if (Data.ProjectId == Guid.Empty)
                    await Fail("Could not identity project of Catlet.");
                else
                    await _taskDispatcher.StartNew(Data.OperationId, new UpdateCatletNetworksCommand
                    {
                        CatletId = Data.CatletId,
                        Config = Data.Config,
                        ProjectId = Data.ProjectId,
                    });
            });
        }


        public Task Handle(OperationTaskStatusEvent<UpdateCatletNetworksCommand> message)
        {

            return FailOrRun<UpdateCatletNetworksCommand, UpdateCatletNetworksCommandResponse>(message,
                async r =>
            {

                var optionalMachineData = await (
                    from vm in _vmDataService.GetVM(Data.CatletId)
                    from metadata in _metadataService.GetMetadata(vm.MetadataId)
                    select (vm, metadata));


                await optionalMachineData.Match(
                    Some: data =>
                    {
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
                    { ErrorMessage = $"Could not find virtual catlet with catlet id {Data.CatletId}" })
                );

            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineCommand> message)
        {
            if (Data.Updated)
                return Task.CompletedTask;

            return FailOrRun<UpdateVirtualMachineCommand, ConvergeVirtualCatletResult>(message, async r =>
            {
                Data.Updated = true;

                await _metadataService.SaveMetadata(r.MachineMetadata);

                await Bus.Send(new UpdateInventoryCommand
                {
                    AgentName = Data.AgentName,
                    Inventory = new List<VirtualMachineData> { r.Inventory }
                });

                await _vmDataService.GetVM(Data.CatletId).Match(
                    Some: data =>
                    {
                        return _taskDispatcher.StartNew(Data.OperationId, new UpdateVirtualCatletConfigDriveCommand
                        {
                            VMId = r.Inventory.VMId,
                            CatletId = Data.CatletId,
                            MachineMetadata = r.MachineMetadata,
                        });
                    },
                    None: () => Fail(new ErrorData
                    { ErrorMessage = $"Could not find virtual catlet with catlet id {Data.CatletId}" })
                );

            });
        }


        public Task Handle(OperationTaskStatusEvent<UpdateVirtualCatletConfigDriveCommand> message)
        {
            return FailOrRun(message, () =>
                _taskDispatcher.StartNew(Data.OperationId, new UpdateNetworksCommand
                {
                    Projects = new[] { Data.ProjectId }
                })

                );

        }

        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }
    }
}