using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
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
using Rebus.Pipeline;
using Rebus.Sagas;
using static System.Net.Mime.MediaTypeNames;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class CreateCatletSaga : OperationTaskWorkflowSaga<CreateCatletCommand, CreateCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceVirtualCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateVCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareVirtualMachineImageCommand>>

    {
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IStateStore _stateStore;

        public CreateCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IMessageContext messageContext, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService, IStateStore stateStore) : base(bus, taskDispatcher, messageContext)
        {
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _stateStore = stateStore;
        }

        public Task Handle(OperationTaskStatusEvent<CreateVCatletCommand> message)
        {
            if (Data.State >= CreateVMState.Created)
                return Task.CompletedTask;

            return FailOrRun<CreateVCatletCommand, ConvergeVirtualCatletResult>(message, async r =>
            {
                Data.State = CreateVMState.Created;

                var tenantId = EryphConstants.DefaultTenantId;
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

            return FailOrRun<PlaceVirtualCatletCommand, PlaceVirtualCatletResult>(message,
                async r =>
            {
                Data.State = CreateVMState.Placed;
                Data.AgentName = r.AgentName;

                Data.ImageNames = new List<string>();
                if(!string.IsNullOrWhiteSpace(Data.Config?.VCatlet?.Image))
                    Data.ImageNames.Add(Data.Config?.VCatlet?.Image??"");

                Data.ImageNames.AddRange( Data.Config?.VCatlet?.Drives
                    .Select(x => x.Template)
                    .Where(t => t!=null && t.StartsWith("image:") && t.Split(':').Length>=2)
                    .Select(t =>t.Split(':')[1]) ?? Enumerable.Empty<string>());

                // no images required - go directly to create
                if (Data.ImageNames.Count == 0)
                {
                    await CreateCatlet();
                    return;
                }

                Data.ImageNames = Data.ImageNames.Distinct().ToList();

                foreach (var imageName in Data.ImageNames)
                {
                    await StartNewTask(new PrepareVirtualMachineImageCommand
                    {
                        Image = imageName,
                        AgentName = r.AgentName
                    });
                }

            });
        }

        public Task Handle(OperationTaskStatusEvent<PrepareVirtualMachineImageCommand> message)
        {
            if (Data.State >= CreateVMState.ImagePrepared)
                return Task.CompletedTask;

            return FailOrRun<PrepareVirtualMachineImageCommand, PrepareVirtualMachineImageResponse>(message, 
                (response) =>
            {

                if (Data.Config != null)
                {
                    if(Data.Config.VCatlet.Image == response.RequestedImage)
                        Data.Config.VCatlet.Image = response.ResolvedImage;

                    foreach (var catletDriveConfig in Data.Config.VCatlet.Drives)
                    {
                        if (catletDriveConfig.Template != null && 
                            catletDriveConfig.Template.StartsWith("image:") &&
                            catletDriveConfig.Template.Contains(response.RequestedImage))
                        {
                            catletDriveConfig.Template = catletDriveConfig.Template.Replace(response.RequestedImage, response.ResolvedImage);
                        }

                    }
                }

                if(Data.ImageNames.Contains(response.RequestedImage))
                    Data.ImageNames.Remove(response.RequestedImage);

                return Data.ImageNames.Count == 0 
                    ? CreateCatlet() 
                    : Task.CompletedTask;
            });
        }

        private Task CreateCatlet()
        {
            Data.State = CreateVMState.ImagePrepared;
            Data.MachineId = Guid.NewGuid();

            return StartNewTask(new CreateVCatletCommand
            {
                Config = Data.Config,
                NewMachineId = Data.MachineId,
                AgentName = Data.AgentName,
                StorageId = _idGenerator.GenerateId()
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
            config.Correlate<OperationTaskStatusEvent<CreateVCatletCommand>>(m => m.InitiatingTaskId,
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