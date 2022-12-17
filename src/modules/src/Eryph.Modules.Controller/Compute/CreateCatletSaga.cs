using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class CreateCatletSaga : OperationTaskWorkflowSaga<CreateCatletCommand, CreateCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceVirtualCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateVirtualCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareVirtualMachineImageCommand>>

    {
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IStateStore _stateStore;

        public CreateCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService, IStateStore stateStore) : base(bus, taskDispatcher)
        {
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _stateStore = stateStore;
        }

        public Task Handle(OperationTaskStatusEvent<CreateVirtualCatletCommand> message)
        {
            if (Data.State >= CreateVMState.Created)
                return Task.CompletedTask;

            return FailOrRun<CreateVirtualCatletCommand, ConvergeVirtualCatletResult>(message, async r =>
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

                await StartNewTask(new UpdateCatletCommand
                {
                    Config = Data.Config,
                    AgentName = Data.AgentName

                },
                    new Resources.Resource(ResourceType.Catlet, Data.MachineId));
            });
        }

        public Task Handle(OperationTaskStatusEvent<PlaceVirtualCatletCommand> message)
        {
            if (Data.State >= CreateVMState.Placed)
                return Task.CompletedTask;

            return FailOrRun<PlaceVirtualCatletCommand, PlaceVirtualCatletResult>(message, r =>
            {
                Data.State = CreateVMState.Placed;
                Data.AgentName = r.AgentName;

                return StartNewTask(new PrepareVirtualMachineImageCommand
                {
                    Image = Data.Config?.VCatlet?.Image,
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

                if (Data.Config != null)
                    Data.Config.VCatlet.Image = image;

                return StartNewTask(new CreateVirtualCatletCommand
                {
                    Config = Data.Config,
                    NewMachineId = Data.MachineId,
                    AgentName = Data.AgentName,
                    StorageId = _idGenerator.GenerateId()
                });
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletCommand> message)
        {
            if (Data.State >= CreateVMState.Updated)
                return Task.CompletedTask;

            return FailOrRun(message, () =>
            {
                Data.State = CreateVMState.Updated;
                return Complete();
            });
        }

        public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
        {
            if (Data.State >= CreateVMState.ConfigValidated)
                return Task.CompletedTask;

            return FailOrRun<ValidateCatletConfigCommand, ValidateCatletConfigCommand>(message, r =>
            {
                Data.Config = r.Config;
                Data.State = CreateVMState.ConfigValidated;


                return StartNewTask(new PlaceVirtualCatletCommand
                    {
                        Config = Data.Config
                    });
            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateCatletSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<PlaceVirtualCatletCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<CreateVirtualCatletCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<PrepareVirtualMachineImageCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override Task Initiated(CreateCatletCommand message)
        {
            Data.Config = message.Config;
            Data.State = CreateVMState.Initiated;

            return StartNewTask(new ValidateCatletConfigCommand
                {
                    MachineId = Guid.Empty,
                    Config = message.Config
                }
            );
        }
    }
}