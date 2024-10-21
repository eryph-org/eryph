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
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
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

using static LanguageExt.Prelude;

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
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>
{
    protected override async Task Initiated(UpdateCatletCommand message)
    {
        Data.Data.State = UpdateVMState.Initiated;
        Data.Data.BredConfig = message.BredConfig;
        Data.Data.ResolvedGenes = message.ResolvedGenes;
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

        var metadata = await metadataService.GetMetadata(machineInfo.MetadataId)
            .Map(m => m.IfNoneUnsafe((CatletMetadata?)null));
        if (metadata is null)
        {
            await Fail($"Catlet cannot be updated because the metadata for catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.Architecture = Architecture.New(metadata.Architecture);

        if (Data.Data.BredConfig is not null && Data.Data.ResolvedGenes is not null)
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
            if (Data.Data.Config is null)
                throw new InvalidOperationException("Config is missing.");

            Data.Data.State = UpdateVMState.Resolved;

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"Metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            var result = PrepareConfigs(
                Data.Data.Config,
                metadata.ValueUnsafe().Metadata,
                response.ResolvedGeneSets.ToHashMap(),
                response.ParentConfigs.ToHashMap());
            if (result.IsLeft)
            {
                await Fail(ErrorUtils.PrintError(Error.Many(result.LeftToSeq())));
                return;
            }

            Data.Data.Config = result.ValueUnsafe().Config;
            Data.Data.BredConfig = result.ValueUnsafe().BredConfig;

            var geneIds = CatletGeneCollecting.CollectGenes(Data.Data.BredConfig);
            if (geneIds.IsFail)
            {
                await Fail(ErrorUtils.PrintError(Error.Many(geneIds.FailToSeq())));
                return;
            }

            await StartNewTask(new ResolveGenesCommand
            {
                AgentName = Data.Data.AgentName,
                CatletArchitecture = Architecture.New(metadata.ValueUnsafe().Metadata.Architecture),
                Genes = geneIds.SuccessToSeq().Flatten().Filter(g => g.GeneType == GeneType.Volume).ToList(),
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveGenesCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.GenesResolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveGenesCommandResponse response) =>
        {
            Data.Data.State = UpdateVMState.GenesResolved;

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"Metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            var resolvedFodderGenes = metadata.ValueUnsafe().Metadata.FodderGenes.ToSeq()
                .Map(kvp => from geneId in GeneIdentifier.NewValidation(kvp.Key)
                            from architecture in Architecture.NewValidation(kvp.Value)
                            select new UniqueGeneIdentifier(GeneType.Fodder, geneId, architecture))
                .Sequence();
            if (resolvedFodderGenes.IsFail)
            {
                await Fail(ErrorUtils.PrintError(Error.New(
                    $"The metadata for catlet {Data.Data.CatletId} contains invalid fodder information",
                    Error.Many(resolvedFodderGenes.FailToSeq()))));
                return;
            }

            // Combine the volumes genes which have been resolved just now with the
            // fodder genes which have been resolved when the catlet has been created.
            // The fodder cannot change after the catlet has been created. Hence, we
            // must use the information from the time of creation of the catlet.
            Data.Data.ResolvedGenes = response.ResolvedGenes
                .Append(resolvedFodderGenes.SuccessToSeq().Flatten())
                .ToList();

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

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = [response.Inventory],
                Timestamp = response.Timestamp,
            });

            if (Data.Data.PendingGenes.Count > 0)
                return;

            await StartUpdateCatlet();
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
                MachineNetworkSettings = r.NetworkSettings,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletVMCommand> message)
    {
        if (Data.Data.State >= UpdateVMState.VMUpdated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ConvergeCatletResult response) =>
        {
            Data.Data.State = UpdateVMState.VMUpdated;


            //TODO: replace this with operation call
            await bus.SendLocal(new UpdateInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = [response.Inventory],
                Timestamp = response.Timestamp,
            });

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

            await StartNewTask(new UpdateCatletConfigDriveCommand
            {
                Config = Data.Data.BredConfig,
                VMId = response.Inventory.VMId,
                CatletId = Data.Data.CatletId,
                MachineMetadata = metadata.ValueUnsafe(),
                ResolvedGenes = Data.Data.ResolvedGenes,
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
        config.Correlate<OperationTaskStatusEvent<ResolveGenesCommand>>(
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

    internal static Validation<Error, Seq<GeneIdentifierWithType>> FindRequiredGenes(
        CatletConfig catletConfig) =>
        CatletGeneCollecting.CollectGenes(catletConfig)
            .Map(l => l.Filter(c => c.GeneIdentifier.GeneName != GeneName.New("catlet")));

    internal static Either<Error, (CatletConfig Config, CatletConfig BredConfig)> PrepareConfigs(
        CatletConfig config,
        CatletMetadata metadata,
        HashMap<GeneSetIdentifier, GeneSetIdentifier> resolvedGeneSets,
        HashMap<GeneSetIdentifier, CatletConfig> parentConfigs) =>
        from resolvedConfig in CatletGeneResolving.ResolveGeneSetIdentifiers(
                config, resolvedGeneSets)
            .MapLeft(e => Error.New("Could not resolve genes in catlet config.", e))
        from breedingResult in CatletPedigree.Breed(
                config, resolvedGeneSets, parentConfigs)
            .MapLeft(e => Error.New("Could not breed the catlet.", e))
        // After the catlet was created, the fodder and variables can no longer be changed.
        // They are used by cloud-init and are only applied on the first startup.
        // To avoid any unexpected behavior, we reuse the fodder and variables from
        // the catlet metadata.
        // In the future, we could consider to diff the fodder and variables and then
        // display a warning to the user in case there were changes.
        let fixedConfig = resolvedConfig.CloneWith(c =>
        {
            c.Fodder = metadata.Fodder.ToSeq().Map(fc => fc.Clone()).ToArray();
            c.Variables = metadata.Variables.ToSeq().Map(vc => vc.Clone()).ToArray();
        })
        let fixedParentConfig = breedingResult.ParentConfig
            .IfNone(new CatletConfig())
            .CloneWith(c =>
            {
                c.Fodder = Optional(metadata.ParentConfig)
                    .Map(c => c.Fodder.ToSeq().Map(fc => fc.Clone()))
                    .IfNone([])
                    .ToArray();
                c.Variables = Optional(metadata.ParentConfig)
                    .Map(c => c.Variables.ToSeq().Map(vc => vc.Clone()))
                    .IfNone([])
                    .ToArray();
            })
        from bredUpdateConfig in CatletBreeding.Breed(fixedParentConfig, fixedConfig)
            .MapLeft(e => Error.New("Could not breed the catlet.", e))
        select (fixedConfig, bredUpdateConfig);

    private async Task StartPrepareGenes()
    {
        Data.Data.State = UpdateVMState.Resolved;

        if (Data.Data.ResolvedGenes!.Count == 0)
        {
            // no images required - go directly to catlet update
            Data.Data.State = UpdateVMState.GenesPrepared;
            Data.Data.PendingGenes = [];
            await StartUpdateCatlet();
            return;
        }

        Data.Data.PendingGenes = Data.Data.ResolvedGenes;
        var commands = Data.Data.ResolvedGenes.Map(id => new PrepareGeneCommand
        {
            AgentName = Data.Data.AgentName,
            Gene = id,
        });

        foreach (var command in commands)
        {
            await StartNewTask(command);
        }
    }

    private Task<Option<(Catlet Catlet, CatletMetadata Metadata)>> GetCatletMetadata(Guid catletId) =>
        from catlet in vmDataService.GetVM(catletId)
        from metadata in metadataService.GetMetadata(catlet.MetadataId)
        select (catlet, metadata);
}
