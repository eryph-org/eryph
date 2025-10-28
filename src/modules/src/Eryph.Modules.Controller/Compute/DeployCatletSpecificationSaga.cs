using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Dbosoft.Functional;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DeployCatletSpecificationSaga(
    IReadonlyStateStoreRepository<Catlet> catletRepository,
    IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IStorageIdentifierGenerator storageIdentifierGenerator,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<DeployCatletSpecificationCommand, EryphSagaData<DeployCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DeployCatletCommand>>
{
    protected override async Task Initiated(DeployCatletSpecificationCommand message)
    {
        var specification = await specificationRepository.GetBySpecAsync(
            new CatletSpecificationSpecs.GetByIdReadOnly(message.SpecificationId));
        if (specification is null)
        {
            await Fail($"The specification {message.SpecificationId} does not exist.");
            return;
        }

        var specificationVersion = await specificationVersionRepository.GetBySpecAsync(
            new CatletSpecificationVersionSpecs.GetByIdReadOnly(message.SpecificationId, message.SpecificationVersionId));
        if (specificationVersion is null)
        {
            await Fail($"The specification version {message.SpecificationVersionId} does not exist.");
            return;
        }

        Data.Data.State = DeployCatletSpecificationSagaState.Initiated;
        Data.Data.SpecificationId = specification.Id;
        Data.Data.SpecificationVersionId = specificationVersion.Id;
        Data.Data.ProjectId = specification.ProjectId;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Redeploy = message.Redeploy;
        Data.Data.ContentType = specificationVersion.ContentType;
        Data.Data.Configuration = specificationVersion.Configuration;
        Data.Data.Architecture = message.Architecture;

        var specificationVersionVariant = specificationVersion.Variants
            .FirstOrDefault(v => v.Architecture == Data.Data.Architecture);
        if (specificationVersionVariant is null)
        {
            await Fail($"The specification version {message.SpecificationVersionId} does not support the architecture {Data.Data.Architecture}.");
            return;
        }

        Data.Data.ResolvedGenes = specificationVersionVariant.PinnedGenes.ToGenesDictionary();
        
        var builtConfig = CatletConfigInstantiator.Instantiate(
            CatletConfigJsonSerializer.Deserialize(specificationVersionVariant.BuiltConfig),
            storageIdentifierGenerator.Generate());

        var updatedVariables = CatletConfigVariableApplier
            .ApplyVariables(builtConfig.Variables.ToSeq(), message.Variables)
            .ToEither()
            .MapLeft(errors => Error.New("Some variables are invalid.", Error.Many(errors)));
        if (updatedVariables.IsLeft)
        {
            await Fail(Error.Many(updatedVariables.LeftToSeq()).Print());
            return;
        }

        Data.Data.BuiltConfig = builtConfig.CloneWith(c =>
        {
            c.Variables = updatedVariables.ValueUnsafe().ToArray();
        });

        var existingCatlet = await catletRepository.GetBySpecAsync(
            new CatletSpecs.GetBySpecificationId(specification.Id));
        if (existingCatlet is null)
        {
            Data.Data.State = DeployCatletSpecificationSagaState.CatletDestroyed;
            await StartNewTask(new ValidateCatletDeploymentCommand
            {
                ProjectId = Data.Data.ProjectId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
            return;
        }

        if (!message.Redeploy)
        {
            await Fail($"The catlet specification {specification.Id} is already deployed as catlet {existingCatlet.Id}.");
            return;
        }

        await StartNewTask(new DestroyCatletCommand
        {
            CatletId = existingCatlet.Id,
        });
    }

    public Task Handle(OperationTaskStatusEvent<DestroyCatletCommand> message)
    {
        if (Data.Data.State >= DeployCatletSpecificationSagaState.CatletDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSpecificationSagaState.CatletDestroyed;

            await StartNewTask(new ValidateCatletDeploymentCommand
            {
                ProjectId = Data.Data.ProjectId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletDeploymentCommand> message)
    {
        if (Data.Data.State >= DeployCatletSpecificationSagaState.DeploymentValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSpecificationSagaState.DeploymentValidated;

            await StartNewTask(new DeployCatletCommand
            {
                ProjectId = Data.Data.ProjectId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                OriginalConfig = Data.Data.Configuration,
                ResolvedGenes = Data.Data.ResolvedGenes,
                SpecificationId = Data.Data.SpecificationId,
                SpecificationVersionId = Data.Data.SpecificationVersionId,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<DeployCatletCommand> message)
    {
        if (Data.Data.State >= DeployCatletSpecificationSagaState.Deployed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DeployCatletCommandResponse response) =>
        {
            Data.Data.State = DeployCatletSpecificationSagaState.Deployed;
            // The ID of the deployed catlet is returned explicitly as
            // multiple catlets might be associated with the operation
            // (the deleted one and the new one).
            await Complete(new DeployCatletSpecificationCommandResponse
            {
                CatletId = response.CatletId,
            });
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<DeployCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<DestroyCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
