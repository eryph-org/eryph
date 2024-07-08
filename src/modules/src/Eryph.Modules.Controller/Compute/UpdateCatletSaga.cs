using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb.Model;
using IdGen;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class UpdateCatletSaga(
    IWorkflow workflow,
    IBus bus,
    IIdGenerator<long> idGenerator,
    IVirtualMachineDataService vmDataService,
    IVirtualMachineMetadataService metadataService)
    : OperationTaskWorkflowSaga<UpdateCatletCommand, EryphSagaData<UpdateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>
{
    protected override async Task Initiated(UpdateCatletCommand message)
    {
        Data.Data.State = UpdateVMState.Initiated;
        Data.Data.BredConfig = message.BreedConfig;
        Data.Data.Config = message.Config;
        Data.Data.CatletId = message.Resource.Id;

        if (Data.Data.CatletId == Guid.Empty)
        {
            await Fail("Catlet cannot be updated because the catlet Id is missing.");
            return;
        }

        var machineInfo = await vmDataService.GetVM(Data.Data.CatletId)
            .Map(m => m.IfNoneUnsafe((Catlet?)null));
        if (machineInfo is null)
        {
            await Fail($"Catlet cannot be updated because the catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.ProjectId = machineInfo.ProjectId;
        Data.Data.AgentName = machineInfo.AgentName;
        Data.Data.TenantId = machineInfo.Project.TenantId;

        if (Data.Data.ProjectId == Guid.Empty)
        {
            await Fail($"Catlet {Data.Data.CatletId} is not assigned to any project.");
            return;
        }

        if (Data.Data.BredConfig is not null)
        {
            // The catlet has already been bred. This happens when the update
            // saga is initiated by the create saga. We can skip directly to
            // the gene preparation step.
            await StartPrepareGenes();
            return;
        }

        await StartNewTask(new ValidateCatletConfigCommand
        {
            MachineId = message.Resource.Id,
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.ConfigValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ValidateCatletConfigCommand response) =>
        {
            Data.Data.State = UpdateVMState.ConfigValidated;
            Data.Data.Config = response.Config;

            await StartNewTask(new ResolveCatletConfigCommand()
            {
                AgentName = Data.Data.AgentName,
                Config = Data.Data.Config,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletConfigCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.Resolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletConfigCommandResponse response) =>
        {
            Data.Data.State = UpdateVMState.Resolved;

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"Metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            var breedingResult = CatletPedigree.Breed(
                Data.Data.Config,
                response.ResolvedGeneSets.ToHashMap(),
                response.ParentConfigs.ToHashMap());

            if (breedingResult.IsLeft)
            {
                await Fail(ErrorUtils.PrintError(Error.New("Could not breed catlet.",
                    Error.Many(breedingResult.LeftToSeq()))));
                return;
            }

            // TODO properly handle updates: fail on changed existing drive or fodder. Only apply other settings.

            Data.Data.BredConfig = breedingResult.ValueUnsafe().Config;

            await StartPrepareGenes();
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.GenesPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareGeneResponse response) =>
        {
            Data.Data.PendingGenes = Data.Data.PendingGenes
                .Except([response.RequestedGene])
                .ToList();

            if (Data.Data.PendingGenes.Count > 0)
                return;

            await StartUpdateCatlet();
            return;
        });
    }

    private async Task StartUpdateCatlet()
    {
        Data.Data.State = UpdateVMState.GenesPrepared;
        await StartNewTask(new UpdateCatletNetworksCommand
        {
            CatletId = Data.Data.CatletId,
            Config = Data.Data.BredConfig,
            ProjectId = Data.Data.ProjectId
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletNetworksCommand> message)
    {
        return FailOrRun(message, async (UpdateCatletNetworksCommandResponse r) =>
        {
            var catlet = await vmDataService.GetVM(Data.Data.CatletId);
            if (catlet.IsNone)
            {
                await Fail($"Could not find catlet with ID {Data.Data.CatletId}.");
                return;
            }

            var metadata = await metadataService.GetMetadata(catlet.ValueUnsafe().MetadataId);
            if (metadata.IsNone)
            {
                await Fail($"Could not find metadata of catlet with ID {Data.Data.CatletId}.");
                return;
            }

            await StartNewTask(new UpdateCatletVMCommand
            {
                CatletId = Data.Data.CatletId,
                VMId = catlet.ValueUnsafe().VMId,
                Config = Data.Data.BredConfig,
                AgentName = Data.Data.AgentName,
                NewStorageId = idGenerator.CreateId(),
                MachineMetadata = metadata.ValueUnsafe(),
                MachineNetworkSettings = r.NetworkSettings
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletVMCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.VMUpdated)
            return Task.CompletedTask;

        return FailOrRun<UpdateCatletVMCommand, ConvergeCatletResult>(message, async r =>
        {
            Data.Data.State = UpdateVMState.VMUpdated;

            await metadataService.SaveMetadata(r.MachineMetadata);

            //TODO: replace this with operation call
            await bus.SendLocal(new UpdateInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = [r.Inventory],
                Timestamp = r.Timestamp,
            });

            var catlet = await vmDataService.GetVM(Data.Data.CatletId);
            if(catlet.IsNone)
            {
                await Fail($"Could not find catlet with ID {Data.Data.CatletId}.");
                return;
            }

            await StartNewTask(new UpdateCatletConfigDriveCommand
            {
                VMId = r.Inventory.VMId,
                CatletId = Data.Data.CatletId,
                CatletName = catlet.ValueUnsafe().Name,
                MachineMetadata = r.MachineMetadata,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.ConfigDriveUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = UpdateVMState.ConfigDriveUpdated;

            await StartNewTask(new UpdateNetworksCommand
            {
                Projects = [Data.Data.ProjectId]
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
    {
        return FailOrRun(message, () => Complete());
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<UpdateCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task StartPrepareGenes()
    {
        if (Data.Data.BredConfig is null)
            throw new InvalidOperationException("Breed config is missing.");

        Data.Data.State = UpdateVMState.Resolved;

        var validation = FindRequiredGenes(Data.Data.BredConfig);
        if (validation.IsFail)
        {
            await Fail(ErrorUtils.PrintError(
                Error.New("Some gene sources are invalid.",
                    Error.Many(validation.FailToSeq()))));
            return;
        }

        var requiredGenes = validation.SuccessToSeq().Flatten();
        if (requiredGenes.IsEmpty)
        {
            // no images required - go directly to catlet update
            Data.Data.State = UpdateVMState.GenesPrepared;
            Data.Data.PendingGenes = [];
            await StartUpdateCatlet();
            return;
        }

        Data.Data.PendingGenes = requiredGenes.ToList();
        var commands = requiredGenes.Map(id => new PrepareGeneCommand
        {
            AgentName = Data.Data.AgentName,
            GeneIdentifier = id,
        });

        foreach (var command in commands)
        {
            await StartNewTask(command);
        }
    }

    internal static Validation<Error, Seq<GeneIdentifierWithType>> FindRequiredGenes(
        CatletConfig catletConfig) =>
        CatletGeneCollecting.CollectGenes(catletConfig)
            .Map(l => l.Filter(c => c.GeneIdentifier.GeneName != GeneName.New("catlet")));

    private Task<Option<(Catlet Catlet, CatletMetadata Metadata)>> GetCatletMetadata(Guid catletId) =>
        from catlet in vmDataService.GetVM(catletId)
        from metadata in metadataService.GetMetadata(catlet.MetadataId)
        select (catlet, metadata);
}
