using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.CatletManagement;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class UpdateCatletSaga(
    IWorkflow workflow,
    IStateStore stateStore,
    ICatletMetadataService metadataService)
    : OperationTaskWorkflowSaga<UpdateCatletCommand, EryphSagaData<UpdateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DeployCatletCommand>>
{
    protected override async Task Initiated(UpdateCatletCommand message)
    {
        Data.Data.State = UpdateCatletSagaState.Initiated;
        Data.Data.CatletId = message.CatletId;

        if (Data.Data.CatletId == Guid.Empty)
        {
            await Fail("Catlet cannot be updated because the catlet Id is missing.");
            return;
        }

        var catlet = await stateStore.Read<Catlet>().GetBySpecAsync(
            new CatletSpecs.GetForConfig(Data.Data.CatletId));
        if (catlet is null)
        {
            await Fail($"Catlet cannot be updated because the catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        Data.Data.ProjectId = catlet.ProjectId;
        Data.Data.AgentName = catlet.AgentName;

        var metadata = await metadataService.GetMetadata(catlet.MetadataId);
        if (metadata is null)
        {
            await Fail($"Catlet cannot be updated because the metadata for catlet {Data.Data.CatletId} does not exist.");
            return;
        }

        if (metadata.IsDeprecated || metadata.Metadata is null)
        {
            await Fail($"Catlet cannot be updated because the catlet {Data.Data.CatletId} has been created with an old version of eryph.");
            return;
        }

        Data.Data.Architecture = metadata.Metadata.Architecture;
        Data.Data.ResolvedGenes = metadata.Metadata.PinnedGenes;

        var networkPorts = await stateStore.Read<CatletNetworkPort>().ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId));

        var validationResult = PrepareConfig(message.Config, metadata.Metadata.Config, catlet, networkPorts);
        if (validationResult.IsLeft)
        {
            await Fail(Error.Many(validationResult.LeftToSeq()).Print());
            return;
        }

        Data.Data.Config = validationResult.ValueUnsafe();

        await StartNewTask(new ValidateCatletDeploymentCommand
        {
            ProjectId = Data.Data.ProjectId,
            AgentName = Data.Data.AgentName,
            Architecture = Data.Data.Architecture,
            Config = Data.Data.Config,
            ResolvedGenes = Data.Data.ResolvedGenes,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletDeploymentCommand> message)
    {
        if (Data.Data.State >= UpdateCatletSagaState.DeploymentValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = UpdateCatletSagaState.DeploymentValidated;

            await StartNewTask(new DeployCatletCommand
            {
                ProjectId = Data.Data.ProjectId,
                CatletId = Data.Data.CatletId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.Config,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<DeployCatletCommand> message)
    {
        if (Data.Data.State >= UpdateCatletSagaState.Deployed)
            return Task.CompletedTask;

        return FailOrRun(message, () =>
        {
            Data.Data.State = UpdateCatletSagaState.Deployed;
            return Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<UpdateCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Either<Error, CatletConfig> PrepareConfig(
        CatletConfig updateConfig,
        CatletConfig originalConfig,
        Catlet catlet,
        IList<CatletNetworkPort> networkPorts) =>
        from _ in Right<Error, Unit>(unit)
        let currentConfig = CatletConfigGenerator.Generate(catlet, networkPorts.ToSeq().Strict(), originalConfig)
        from _2 in CatletUpdateValidator.Validate(updateConfig, currentConfig)
            .ToEitherWithJsonPath(
                $"The updated configuration for the catlet {catlet.Id} is invalid.",
                CatletConfigJsonSerializer.Options.PropertyNamingPolicy)
        from normalizedConfig in CatletConfigNormalizer.Normalize(updateConfig)
            .ToEither()
            .MapLeft(errors => Error.New(
                $"The updated configuration for the catlet {catlet.Id} cannot be normalized.",
                Error.Many(errors)))
        let instantiatedConfig = CatletConfigInstantiator.InstantiateUpdate(normalizedConfig)
        select instantiatedConfig;
}
