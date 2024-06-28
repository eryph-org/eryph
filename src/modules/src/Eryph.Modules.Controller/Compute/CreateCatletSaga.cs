using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using IdGen;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class CreateCatletSaga : OperationTaskWorkflowSaga<CreateCatletCommand, CreateCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<BreedCatletCommand>>
    {
        private readonly IIdGenerator<long> _idGenerator;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IStateStore _stateStore;

        public CreateCatletSaga(IWorkflow workflow, IIdGenerator<long> idGenerator,
            IVirtualMachineDataService vmDataService, IStateStore stateStore) : base(workflow)
        {
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _stateStore = stateStore;
        }

        public Task Handle(OperationTaskStatusEvent<CreateCatletVMCommand> message)
        {
            if (Data.State >= CreateVMState.Created)
                return Task.CompletedTask;

            return FailOrRun<CreateCatletVMCommand, ConvergeCatletResult>(message, async r =>
            {
                Data.State = CreateVMState.Created;

                var projectName = Optional(Data.Config?.Project).Filter(notEmpty).Match(
                    Some: n => ProjectName.New(n),
                    None: () => ProjectName.New("default"));

                var environmentName = Optional(Data.Config?.Environment).Filter(notEmpty).Match(
                    Some: n => EnvironmentName.New(n),
                    None: () => EnvironmentName.New("default"));

                var datastoreName = Optional(Data.Config?.Store).Filter(notEmpty).Match(
                    Some: n => DataStoreName.New(n),
                    None: () => DataStoreName.New("default"));

                var project = await _stateStore.For<Project>()
                    .GetBySpecAsync(new ProjectSpecs.GetByName(Data.TenantId, projectName.Value));

                if (project == null)
                    throw new InvalidOperationException($"Project '{projectName}' not found.");

                r.MachineMetadata.ParentConfig = Data.ParentConfig;

                _ = await _vmDataService.AddNewVM(new Catlet
                {
                    ProjectId = project.Id,
                    Id = Data.MachineId,
                    AgentName = Data.AgentName,
                    VMId = r.Inventory.VMId,
                    Name = r.Inventory.Name,
                    Environment = environmentName.Value,
                    DataStore = datastoreName.Value,
                }, r.MachineMetadata);

                await StartNewTask(new UpdateCatletCommand
                {
                    Config = Data.Config,
                    BreedConfig = Data.BredConfig,
                    CatletId = Data.MachineId
                });
            });
        }

        public Task Handle(OperationTaskStatusEvent<PlaceCatletCommand> message)
        {
            if (Data.State >= CreateVMState.Placed)
                return Task.CompletedTask;

            return FailOrRun<PlaceCatletCommand, PlaceCatletResult>(message,
                async r =>
            {
                Data.State = CreateVMState.Placed;
                Data.AgentName = r.AgentName;


                // TODO Remove this? We always need to breed as it also resolve gene references
                if (string.IsNullOrWhiteSpace(Data.Config?.Parent))
                {
                    await CreateCatlet();
                    return;

                }

                await StartNewTask(new BreedCatletCommand()
                {
                    AgentName = r.AgentName,
                    Config = Data.Config,
                });

                //Data.GeneSetName = Data.Config?.Parent??"";

                //Data.GeneNames.AddRange( (Data.Config?.Drives?
                //    .Select(x => x.Source)
                //    .Where(t => t!=null && t.StartsWith("gene:") && t.Split(':').Length>=2) ?? Array.Empty<string>()));

                //// no genesets required - go directly to create
                //if (Data.GeneNames.Count == 0 && string.IsNullOrWhiteSpace(Data.GeneSetName))
                //{

                //}

                //if (!string.IsNullOrWhiteSpace(Data.GeneSetName))
                //{

                //}

                //Data.GeneNames = Data.GeneNames.Distinct().ToList();

                //foreach (var geneName in Data.GeneNames)
                //{
                //    await StartNewTask(new PrepareGeneSetCommand
                //    {
                //        GeneName = geneName,
                //        AgentName = r.AgentName
                //    });
                //}

            });
        }

        private Task CreateCatlet()
        {
            Data.State = CreateVMState.ImagePrepared;
            Data.MachineId = Guid.NewGuid();

            return StartNewTask(new CreateCatletVMCommand
            {
                Config = Data.Config,
                BreedConfig = Data.BredConfig,
                NewMachineId = Data.MachineId,
                AgentName = Data.AgentName,
                StorageId = _idGenerator.CreateId()
            }).AsTask();
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


                return StartNewTask(new PlaceCatletCommand
                    {
                        Config = Data.Config
                    }).AsTask();
            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateCatletSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<PlaceCatletCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<CreateCatletVMCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<BreedCatletCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override Task Initiated(CreateCatletCommand message)
        {
            Data.Config = message.Config;
            Data.State = CreateVMState.Initiated;
            Data.TenantId = message.TenantId;
            return StartNewTask(new ValidateCatletConfigCommand
                {
                    MachineId = Guid.Empty,
                    Config = message.Config
                }
            ).AsTask();
        }

        public Task Handle(OperationTaskStatusEvent<BreedCatletCommand> message)
        {
            if (Data.State >= CreateVMState.ImagePrepared)
                return Task.CompletedTask;

            return FailOrRun<BreedCatletCommand, BreedCatletCommandResponse>(
                message,
                (response) =>
                {
                    Data.BredConfig = response.BreedConfig;
                    Data.ParentConfig = response.ParentConfig;
                    return CreateCatlet();
                });
        }
    }
}