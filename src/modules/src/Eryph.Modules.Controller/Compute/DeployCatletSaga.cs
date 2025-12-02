using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

// This saga is responsible for deploying a catlet based on a specification.
[UsedImplicitly]
internal class DeployCatletSaga(
    IWorkflow workflow,
    IBus bus,
    IInventoryLockManager lockManager,
    ICatletDataService catletDataService,
    ICatletMetadataService metadataService,
    IStateStoreRepository<Catlet> catletRepository,
    IStateStoreRepository<Project> projectRepository)
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
        Data.Data.ProjectId = message.ProjectId;
        Data.Data.AgentName = message.AgentName;
        Data.Data.Architecture = message.Architecture;
        Data.Data.ContentType = message.ContentType;
        Data.Data.OriginalConfig = message.OriginalConfig;
        Data.Data.ResolvedGenes = message.ResolvedGenes;
        Data.Data.SpecificationId = message.SpecificationId;
        Data.Data.SpecificationVersionId = message.SpecificationVersionId;

        if (!message.CatletId.HasValue)
        {
            var project = await projectRepository.GetByIdAsync(Data.Data.ProjectId);
            if (project is null)
            {
                await Fail($"The project {Data.Data.ProjectId} does not exist..");
                return;
            }

            Data.Data.Config = message.Config.CloneWith(c =>
            {
                c.Project = project.Name;
            });

            if (Data.Data.SpecificationId.HasValue)
            {
                var deployedCatlet = await catletRepository.GetBySpecAsync(
                    new CatletSpecs.GetBySpecificationId(Data.Data.SpecificationId.Value));
                if (deployedCatlet is not null)
                {
                    await Fail($"The specification {Data.Data.SpecificationId} is already deployed as catlet {deployedCatlet.Id}.");
                    return;
                }
            }
            
            Data.Data.CatletId = Guid.NewGuid();
            Data.Data.MetadataId = Guid.NewGuid();
            await StartNewTask(new CreateCatletVMCommand
            {
                CatletId = Data.Data.CatletId,
                MetadataId = Data.Data.MetadataId,
                // We can use the config without feeding the system variables
                // as the create command only uses small subset of the config
                // which is independent of the system variables.
                Config = Data.Data.Config,
                AgentName = Data.Data.AgentName,
            });
            return;
        }

        Data.Data.CatletId = message.CatletId.Value;

        var catlet = await catletDataService.Get(Data.Data.CatletId);
        if (catlet is null)
        {
            await Fail($"The catlet {Data.Data.CatletId} was not found.");
            return;
        }

        Data.Data.MetadataId = catlet.MetadataId;
        Data.Data.VmId = catlet.VmId;
        Data.Data.Config = message.Config.CloneWith(c =>
        {
            c.Project = catlet.Project.Name;
        });

        await StartNewTask(new UpdateCatletNetworksCommand
        {
            CatletId = Data.Data.CatletId,
            CatletMetadataId = Data.Data.MetadataId,
            Config = CatletSystemDataFeeding.FeedSystemVariables(
                Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
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

            Data.Data.VmId = response.Inventory.VmId;

            var catletMetadata = new CatletMetadataContent
            {
                Config = Data.Data.Config,
                Architecture = Data.Data.Architecture,
                PinnedGenes = Data.Data.ResolvedGenes,
                OriginalConfig = Data.Data.OriginalConfig!.ReplaceLineEndings("\n"),
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
                    SpecificationId = Data.Data.SpecificationId,
                    SpecificationVersionId = Data.Data.SpecificationVersionId,
                });

            await catletDataService.Add(new Catlet
            {
                ProjectId = Data.Data.ProjectId,
                Id = Data.Data.CatletId,
                MetadataId = Data.Data.MetadataId,
                AgentName = Data.Data.AgentName,
                VmId = response.Inventory.VmId,
                Name = response.Inventory.Name,
                Environment = Data.Data.Config!.Environment!,
                DataStore = Data.Data.Config!.Store!,
                StorageIdentifier = Data.Data.Config!.Location!,
                // Ensure that any inventory updates are applied as the
                // information which we save right now is incomplete.
                LastSeen = DateTimeOffset.MinValue,
                LastSeenState = DateTimeOffset.MinValue,
                SpecificationId = Data.Data.SpecificationId,
                SpecificationVersionId = Data.Data.SpecificationVersionId,
            });

            await StartNewTask(new UpdateCatletNetworksCommand
            {
                CatletId = Data.Data.CatletId,
                CatletMetadataId = Data.Data.MetadataId,
                Config = CatletSystemDataFeeding.FeedSystemVariables(
                    Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
                ProjectId = Data.Data.ProjectId,
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

            var catlet = await catletDataService.Get(Data.Data.CatletId);
            if (catlet is null)
            {
                await Fail($"The catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new UpdateCatletVMCommand
            {
                CatletId = Data.Data.CatletId,
                VmId = catlet.VmId,
                MetadataId = Data.Data.MetadataId,
                Config = CatletSystemDataFeeding.FeedSystemVariables(
                    Data.Data.Config, Data.Data.CatletId, Data.Data.VmId),
                AgentName = Data.Data.AgentName,
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

            var catlet = await catletDataService.Get(Data.Data.CatletId);
            if (catlet is null)
            {
                await Fail($"The catlet {Data.Data.CatletId} was not found.");
                return;
            }

            await StartNewTask(new SyncVmNetworkPortsCommand
            {
                CatletId = Data.Data.CatletId,
                VmId = catlet.VmId,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<SyncVmNetworkPortsCommand> message)
    {
        if (Data.Data.State >= DeployCatletSagaState.NetworkPortsSynced)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSagaState.NetworkPortsSynced;
            await Complete(new DeployCatletCommandResponse
            {
                CatletId = Data.Data.CatletId,
            });
        });
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
