using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core.Genetics;
using Eryph.Core;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel;
using Eryph.StateDb.Model;
using LanguageExt.UnsafeValueAccess;

using static LanguageExt.Prelude;

using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class PrepareCatletConfigSaga(
    IBus bus,
    IWorkflow workflow,
    IVirtualMachineDataService vmDataService,
    IVirtualMachineMetadataService metadataService)
    : OperationTaskWorkflowSaga<PrepareCatletConfigCommand, EryphSagaData<PrepareCatletConfigSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveGenesCommand>>

{
    protected override async Task Initiated(PrepareCatletConfigCommand message)
    {
        Data.Data.State = PrepareCatletConfigState.Initiated;
        Data.Data.CatletId = message.CatletId;
        Data.Data.Config = message.Config;

        if (Data.Data.CatletId == Guid.Empty)
        {
            await Fail("Config for existing catlet cannot be prepared because the catlet Id is missing.");
            return;
        }

        var machineInfo = await vmDataService.GetVM(Data.Data.CatletId)
            .Map(m => m.IfNoneUnsafe((Catlet?)null));
        if (machineInfo is null)
        {
            await Fail($"Config for existing catlet cannot be prepared because the catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.AgentName = machineInfo.AgentName;

        await StartNewTask(new ValidateCatletConfigCommand
        {
            MachineId = message.Resource.Id,
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
    {
        if (Data.Data.State >= PrepareCatletConfigState.ConfigValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ValidateCatletConfigCommand response) =>
        {
            Data.Data.State = PrepareCatletConfigState.ConfigValidated;
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
        if (Data.Data.State >= PrepareCatletConfigState.Resolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletConfigCommandResponse response) =>
        {
            if (Data.Data.Config is null)
                throw new InvalidOperationException("Config is missing.");
            Data.Data.State = PrepareCatletConfigState.Resolved;

            await bus.SendLocal(new UpdateGenesInventoryCommand
            {
                AgentName = Data.Data.AgentName,
                Inventory = response.Inventory,
                Timestamp = response.Timestamp,
            });

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"The metadata for catlet {Data.Data.CatletId} was not found.");
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
                Genes = geneIds.SuccessToSeq().Flatten()
                    .Filter(g => g.GeneType == GeneType.Volume)
                    .ToList(),
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveGenesCommand> message)
    {
        if (Data.Data.State >= PrepareCatletConfigState.GenesResolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveGenesCommandResponse response) =>
        {
            Data.Data.State = PrepareCatletConfigState.GenesResolved;

            var metadata = await GetCatletMetadata(Data.Data.CatletId);
            if (metadata.IsNone)
            {
                await Fail($"The metadata for catlet {Data.Data.CatletId} was not found.");
                return;
            }

            var resolvedFodderGenes = metadata.ValueUnsafe().Metadata.ResolvedFodderGenes.ToSeq()
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

            await Complete(new PrepareCatletConfigCommandResponse
            {
                Config = Data.Data.Config,
                BredConfig = Data.Data.BredConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

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

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<PrepareCatletConfigSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveGenesCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Task<Option<(Catlet Catlet, CatletMetadata Metadata)>> GetCatletMetadata(Guid catletId) =>
        from catlet in vmDataService.GetVM(catletId)
        from metadata in metadataService.GetMetadata(catlet.MetadataId)
        select (catlet, metadata);
}
