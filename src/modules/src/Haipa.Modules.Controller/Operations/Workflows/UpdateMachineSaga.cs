using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations.Events;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.ModuleCore;
using Haipa.Modules.Controller.DataServices;
using Haipa.Modules.Controller.IdGenerator;
using Haipa.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class UpdateMachineSaga : OperationTaskWorkflowSaga<UpdateMachineCommand, UpdateMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateMachineConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>
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

                await Complete();
            });
        }

        public Task Handle(OperationTaskStatusEvent<ValidateMachineConfigCommand> message)
        {
            if (Data.Validated)
                return Task.CompletedTask;

            return FailOrRun<ValidateMachineConfigCommand, ValidateMachineConfigCommand>(message, async r =>
            {
                Data.Config = r.Config;

                var optionalMachineData = await
                    from vm in _vmDataService.GetVM(Data.MachineId)
                    from metadata in _metadataService.GetMetadata(vm.MetadataId)
                    select (vm, metadata);

                await optionalMachineData.Match(
                    Some: data =>
                    {
                        Data.Validated = true;

                        return _taskDispatcher.StartNew(Data.OperationId, new UpdateVirtualMachineCommand
                        {
                            VMId = data.vm.VMId,
                            Config = Data.Config,
                            AgentName = Data.AgentName,
                            NewStorageId = _idGenerator.GenerateId(),
                            MachineMetadata = data.metadata,
                        });
                    },
                    None: () => Fail(new ErrorData
                        {ErrorMessage = $"Could not find virtual machine with machine id {Data.MachineId}"})
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
        }

        public override Task Initiated(UpdateMachineCommand message)
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