using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using IdGen;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Linq;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

// This saga is responsible for deploying a catlet based on a specification.
[UsedImplicitly]
internal class DeployCatletSaga(
    IWorkflow workflow,
    IBus bus,
    IIdGenerator<long> idGenerator,
    IInventoryLockManager lockManager,
    IStateStore stateStore,
    IVirtualMachineDataService vmDataService,
    IVirtualMachineMetadataService metadataService)
    : OperationTaskWorkflowSaga<DeployCatletCommand, EryphSagaData<DeployCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<CreateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<SyncVmNetworkPortsCommand>>
{
    protected override async Task Initiated(DeployCatletCommand message)
    {
        Data.Data.State = DeployCatletSagaState.Initiated;
        Data.Data.TenantId = message.TenantId;
        Data.Data.AgentName = message.AgentName;
        Data.Data.Architecture = message.Architecture;
        Data.Data.Config = message.Config;
        Data.Data.ResolvedGenes = message.ResolvedGenes;

        // TODO Should we know the project ID already?
        // TODO What happens if the project is changed?

        if (!message.CatletId.HasValue)
        {
            Data.Data.CatletId = Guid.NewGuid();
            Data.Data.MetadataId = Guid.NewGuid();
            await StartNewTask(new CreateCatletVMCommand
            {
                CatletId = Data.Data.CatletId,
                MetadataId = Data.Data.MetadataId,
                Config = Data.Data.Config,
                AgentName = Data.Data.AgentName,
                StorageId = idGenerator.CreateId(),
            });
            return;
        }

        Data.Data.CatletId = message.CatletId.Value;
        var catlet = await vmDataService.GetVM(Data.Data.CatletId)
            .Map(o => o.IfNoneUnsafe((Catlet?)null));
        if (catlet is null)
        {
            await Fail($"The catlet {Data.Data.CatletId} was not found.");
            return;
        }

        Data.Data.MetadataId = catlet.MetadataId;
        Data.Data.VmId = catlet.VMId;
        Data.Data.ProjectId = catlet.ProjectId;

        await StartNewTask(new UpdateCatletNetworksCommand
        {
            CatletId = Data.Data.CatletId,
            CatletMetadataId = Data.Data.MetadataId,
            Config = Data.Data.Config,
            ProjectId = Data.Data.ProjectId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<CreateCatletVMCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.VmCreated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ConvergeCatletResult response) =>
        {
            await lockManager.AcquireVmLock(response.VmId);
            Data.Data.State = DeployCatletSagaState.VmCreated;

            var projectName = Optional(Data.Data.Config?.Project).Filter(notEmpty).Match(
                Some: n => ProjectName.New(n),
                None: () => ProjectName.New("default"));

            var environmentName = Optional(Data.Data.Config?.Environment).Filter(notEmpty).Match(
                Some: n => EnvironmentName.New(n),
                None: () => EnvironmentName.New("default"));

            var datastoreName = Optional(Data.Data.Config?.Store).Filter(notEmpty).Match(
                Some: n => DataStoreName.New(n),
                None: () => DataStoreName.New("default"));

            var project = await stateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(Data.Data.TenantId, projectName.Value));

            if (project == null)
                throw new InvalidOperationException($"Project '{projectName}' not found.");

            Data.Data.ProjectId = project.Id;
            Data.Data.VmId = response.Inventory.VmId;

            var catletMetadata = new CatletMetadataContent
            {
                BuiltConfig = Data.Data.Config,
                Architecture = Data.Data.Architecture,
                PinnedGenes = Data.Data.ResolvedGenes,
                // TODO save YAML
                ConfigYaml = "missing",
            };

            await metadataService.AddMetadata(
                new CatletMetadata
                {
                    Id = Data.Data.MetadataId,
                    CatletId = Data.Data.CatletId,
                    VmId = response.Inventory.VmId,
                    Metadata = catletMetadata,
                    IsDeprecated = false,
                    SecretDataHidden = false,
                });

            var savedCatlet = await vmDataService.AddNewVM(new Catlet
            {
                ProjectId = project.Id,
                Id = Data.Data.CatletId,
                MetadataId = Data.Data.MetadataId,
                AgentName = Data.Data.AgentName,
                VMId = response.Inventory.VmId,
                Name = response.Inventory.Name,
                Environment = environmentName.Value,
                DataStore = datastoreName.Value,
                // Ensure that any inventory updates are applied as the
                // information which we save right now is incomplete.
                LastSeen = DateTimeOffset.MinValue,
                LastSeenState = DateTimeOffset.MinValue,
            });

            await StartNewTask(new UpdateCatletNetworksCommand
            {
                CatletId = Data.Data.CatletId,
                CatletMetadataId = savedCatlet.MetadataId,
                Config = CatletSystemDataFeeding.FeedSystemVariables(
                    Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
                ProjectId = project.Id,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletNetworksCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.CatletNetworksUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async (UpdateCatletNetworksCommandResponse r) =>
        {
            Data.Data.State = DeployCatletSagaState.CatletNetworksUpdated;

            var catlet = await vmDataService.GetVM(Data.Data.CatletId)
                .Map(o => o.IfNoneUnsafe((Catlet?)null));
            if (catlet is null)
            {
                await Fail($"The catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new UpdateCatletVMCommand
            {
                CatletId = Data.Data.CatletId,
                VmId = catlet.VMId,
                MetadataId = Data.Data.MetadataId,
                Config = CatletSystemDataFeeding.FeedSystemVariables(
                    Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
                AgentName = Data.Data.AgentName,
                NewStorageId = idGenerator.CreateId(),
                MachineNetworkSettings = r.NetworkSettings,
                ResolvedGenes = Data.Data.ResolvedGenes.Keys.ToList(),
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletVMCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.VmUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ConvergeCatletResult response) =>
        {
            Data.Data.State = DeployCatletSagaState.VmUpdated;

            //TODO: replace this with operation call
            await bus.SendLocal(new UpdateInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            var metadata = await metadataService.GetMetadata(Data.Data.MetadataId);
            if (metadata is null)
            {
                await Fail($"The metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new UpdateCatletConfigDriveCommand
            {
                Config = CatletSystemDataFeeding.FeedSystemVariables(
                    Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
                VmId = response.Inventory.VmId,
                CatletId = Data.Data.CatletId,
                MetadataId = Data.Data.MetadataId,
                SecretDataHidden = metadata.SecretDataHidden,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.ConfigDriveUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSagaState.ConfigDriveUpdated;

            await StartNewTask(new UpdateNetworksCommand
            {
                Projects = [Data.Data.ProjectId]
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.NetworksUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSagaState.NetworksUpdated;

            var catlet = await vmDataService.GetVM(Data.Data.CatletId)
                .Map(o => o.IfNoneUnsafe((Catlet?)null));
            if (catlet is null)
            {
                await Fail($"The catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new SyncVmNetworkPortsCommand
            {
                CatletId = Data.Data.CatletId,
                VmId = catlet.VMId,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<SyncVmNetworkPortsCommand> message)
    {
        return FailOrRun(message, Complete);
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<DeployCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<CreateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<SyncVmNetworkPortsCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
