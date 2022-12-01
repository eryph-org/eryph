using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class CreateMachineSaga : OperationTaskWorkflowSaga<CreateMachineCommand, CreateMachineSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateMachineConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceVirtualMachineCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateVirtualMachineCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateMachineCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareVirtualMachineImageCommand>>

    {
        private readonly Id64Generator _idGenerator;
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IStateStore _stateStore;

        public CreateMachineSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService, IStateStore stateStore) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _stateStore = stateStore;
        }

        public Task Handle(OperationTaskStatusEvent<CreateVirtualMachineCommand> message)
        {
            if (Data.State >= CreateVMState.Created)
                return Task.CompletedTask;

            return FailOrRun<CreateVirtualMachineCommand, ConvergeVirtualMachineResult>(message, async r =>
            {
                Data.State = CreateVMState.Created;

                var tenantId = Guid.Parse("{C1813384-8ECB-4F17-B846-821EE515D19B}");
                var projectName = Data.Config?.Project ?? "default";

                var project = await _stateStore.For<Project>()
                    .GetBySpecAsync(new ProjectSpecs.GetByName(tenantId, projectName));
                
                if (project == null)
                    throw new InvalidOperationException($"Project '{projectName}' not found.");


                _ = await _vmDataService.AddNewVM(new VirtualCatlet
                {
                    ProjectId = project.Id,
                    Id = Data.MachineId,
                    AgentName = Data.AgentName,
                    VMId = r.Inventory.VMId
                }, r.MachineMetadata);

                await _taskDispatcher.StartNew(Data.OperationId, new UpdateMachineCommand
                {
                    Config = Data.Config,
                    AgentName = Data.AgentName

                }, 
                    new Resources.Resource(ResourceType.Machine, Data.MachineId));
            });
        }

        public Task Handle(OperationTaskStatusEvent<PlaceVirtualMachineCommand> message)
        {
            if (Data.State >= CreateVMState.Placed)
                return Task.CompletedTask;

            return FailOrRun<PlaceVirtualMachineCommand, PlaceVirtualMachineResult>(message, r =>
            {
                Data.State = CreateVMState.Placed;
                Data.AgentName = r.AgentName;

                return _taskDispatcher.StartNew(Data.OperationId,new PrepareVirtualMachineImageCommand
                {
                    Image = Data.Config?.VM?.Image,
                    AgentName = r.AgentName
                });
            });
        }

        public Task Handle(OperationTaskStatusEvent<PrepareVirtualMachineImageCommand> message)
        {
            if (Data.State >= CreateVMState.ImagePrepared)
                return Task.CompletedTask;

            return FailOrRun<PrepareVirtualMachineImageCommand, string>(message, (image) =>
            {
                Data.State = CreateVMState.ImagePrepared;
                Data.MachineId = Guid.NewGuid();

                if(Data.Config!= null)
                    Data.Config.VM.Image = image;

                return _taskDispatcher.StartNew(Data.OperationId,new CreateVirtualMachineCommand
                {
                    Config = Data.Config,
                    NewMachineId = Data.MachineId,
                    AgentName = Data.AgentName,
                    StorageId = _idGenerator.GenerateId()
                });
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateMachineCommand> message)
        {
            if (Data.State >= CreateVMState.Updated)
                return Task.CompletedTask;

            return FailOrRun(message, () =>
            {
                Data.State = CreateVMState.Updated;
                return Complete();
            });
        }

        public Task Handle(OperationTaskStatusEvent<ValidateMachineConfigCommand> message)
        {
            if (Data.State >= CreateVMState.ConfigValidated)
                return Task.CompletedTask;

            return FailOrRun<ValidateMachineConfigCommand, ValidateMachineConfigCommand>(message, r =>
            {
                Data.Config = r.Config;
                Data.State = CreateVMState.ConfigValidated;


                return _taskDispatcher.StartNew(Data.OperationId,
                    new PlaceVirtualMachineCommand
                    {
                        Config = Data.Config
                    });
            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateMachineSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<ValidateMachineConfigCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<PlaceVirtualMachineCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<CreateVirtualMachineCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<PrepareVirtualMachineImageCommand>>(m => m.OperationId,
                d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<UpdateMachineCommand>>(m => m.OperationId, d => d.OperationId);
        }


        protected override Task Initiated(CreateMachineCommand message)
        {
            Data.Config = message.Config;
            Data.State = CreateVMState.Initiated;

            return _taskDispatcher.StartNew(Data.OperationId,
                new ValidateMachineConfigCommand
                {
                    MachineId = Guid.Empty,
                    Config = message.Config
                }
            );
        }
    }
}