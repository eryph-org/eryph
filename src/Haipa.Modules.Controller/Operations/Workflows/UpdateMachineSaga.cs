using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Events;
using Haipa.Messages.Operations;
using Haipa.Modules.Controller.IdGenerator;
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
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;


        public UpdateMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, Id64Generator idGenerator, 
            IVirtualMachineDataService vmDataService, 
            IVirtualMachineMetadataService metadataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateMachineSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<ValidateMachineConfigCommand>>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateVirtualMachineCommand>>(m => m.OperationId, d => d.OperationId);

        }

        public override Task Initiated(UpdateMachineCommand message)
        {
            Data.Config = message.Config;
            Data.MachineId = message.MachineId;
            Data.AgentName = message.AgentName;


            return _taskDispatcher.Send(
                new ValidateMachineConfigCommand
                {
                    MachineId = message.MachineId,
                    Config = message.Config,
                    OperationId = Data.OperationId,
                    TaskId = Guid.NewGuid(),
                }
            );
        }

        public Task Handle(OperationTaskStatusEvent<ValidateMachineConfigCommand> message)
        {
            if (Data.Validated)
                return Task.CompletedTask;

            return FailOrRun<ValidateMachineConfigCommand, ValidateMachineConfigCommand>(message,async (r) =>
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
                        var convergeMessage = new UpdateVirtualMachineCommand
                        {
                            MachineId = data.vm.Id,
                            Config = Data.Config,
                            AgentName = Data.AgentName,
                            OperationId = message.OperationId,
                            NewStorageId = _idGenerator.GenerateId(),
                            MachineMetadata = data.metadata,
                            TaskId = Guid.NewGuid()
                        };

                        return _taskDispatcher.Send(convergeMessage);
                    },
                    None: () => Fail(new ErrorData
                        { ErrorMessage = $"Could not find virtual machine with Id {Data.MachineId}" })
                );


            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateVirtualMachineCommand> message)
        {
            if(Data.Updated)
                return Task.CompletedTask;

            return FailOrRun<UpdateVirtualMachineCommand, ConvergeVirtualMachineResult>(message, async (r) =>
            {
                Data.Updated = true;

                await _metadataService.SaveMetadata(r.MachineMetadata);

                await Bus.Send(new UpdateInventoryCommand
                {
                    AgentName = Data.AgentName,
                    Inventory = new List<MachineInfo> { r.Inventory }
                });

                await Complete();
            });

        }

    }
}