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
using System.Threading.Tasks;
using Eryph.ConfigModel;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Dbosoft.Functional;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DeployCatletSpecificationSaga(
    IReadonlyStateStoreRepository<CatletSpecification> specificationRepository,
    IReadonlyStateStoreRepository<CatletSpecificationVersion> specificationVersionRepository,
    IStorageIdentifierGenerator storageIdentifierGenerator,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<DeployCatletSpecificationCommand, EryphSagaData<DeployCatletSpecificationSagaData>>(workflow),
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
            new CatletSpecificationVersionSpecs.GetLatestBySpecificationIdReadOnly(message.SpecificationId));
        if (specificationVersion is null)
        {
            await Fail($"The specification {message.SpecificationId} has no deployable version.");
            return;
        }

        Data.Data.State = DeployCatletSpecificationSagaState.Initiated;
        Data.Data.SpecificationId = specification.Id;
        Data.Data.SpecificationVersionId = specificationVersion.Id;
        Data.Data.ProjectId = specification.ProjectId;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(specification.Architecture);
        Data.Data.ResolvedGenes = specificationVersion.Genes.ToGenesDictionary();
        Data.Data.ConfigYaml = specificationVersion.ConfigYaml;

        var builtConfig = CatletConfigInstantiator.Instantiate(
            CatletConfigJsonSerializer.Deserialize(specificationVersion.ResolvedConfig),
            storageIdentifierGenerator.Generate());

        builtConfig.Name = specification.Name;

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

        await StartNewTask(new ValidateCatletDeploymentCommand
        {
            ProjectId = Data.Data.ProjectId,
            AgentName = Data.Data.AgentName,
            Architecture = Data.Data.Architecture,
            Config = Data.Data.BuiltConfig,
            ResolvedGenes = Data.Data.ResolvedGenes,
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
                ConfigYaml = Data.Data.ConfigYaml,
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

        return FailOrRun(message, async () =>
        {
            Data.Data.State = DeployCatletSpecificationSagaState.Deployed;
            await Complete();
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<DeployCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
