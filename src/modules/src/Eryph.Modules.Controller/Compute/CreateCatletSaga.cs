using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSaga(
    IStorageIdentifierGenerator storageIdentifierGenerator,
    IStateStore stateStore,
    IPlacementCalculator placementCalculator,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CreateCatletCommand, EryphSagaData<CreateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DeployCatletCommand>>
{
    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = CreateCatletSagaState.SpecificationBuilt;

            var resolvedGenes = response.ResolvedGenes ?? throw new InvalidOperationException("ResolvedGenes from BuildCatletSpecificationCommand must not be null.");
            Data.Data.ResolvedGenes = resolvedGenes;
            var builtConfig = response.BuiltConfig ?? throw new InvalidOperationException("BuiltConfig from BuildCatletSpecificationCommand must not be null.");
            Data.Data.BuiltConfig = CatletConfigInstantiator.Instantiate(
                builtConfig,
                storageIdentifierGenerator.Generate());

            Data.Data.BuiltConfig.Project = Data.Data.ProjectName!.Value;

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

    public Task Handle(OperationTaskStatusEvent<DeployCatletCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.Deployed)
            return Task.CompletedTask;

        return FailOrRun(message, () =>
        {
            Data.Data.State = CreateCatletSagaState.Deployed;
            return Complete();
        });
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletDeploymentCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.DeploymentValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = CreateCatletSagaState.DeploymentValidated;

            await StartNewTask(new DeployCatletCommand
            {
                ProjectId = Data.Data.ProjectId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                ContentType = Data.Data.ContentType,
                OriginalConfig = Data.Data.OriginalConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    protected override async Task Initiated(CreateCatletCommand message)
    {
        Data.Data.State = CreateCatletSagaState.Initiated;
        Data.Data.TenantId = message.TenantId;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);

        var config = message.Config ?? throw new InvalidOperationException("Config from CreateCatletCommand must not be null.");

        var placement = placementCalculator.CalculateVMPlacement(config, Data.Data.Architecture);
        if (placement.IsLeft)
        {
            await Fail(placement.LeftToSeq().Head.Message);
            return;
        }

        Data.Data.AgentName = placement.RightToSeq().Head;

        Data.Data.ProjectName = Optional(config.Project).Filter(notEmpty).Match(
            n => ProjectName.New(n),
            () => ProjectName.New(EryphConstants.DefaultProjectName));
        var project = await stateStore.For<Project>()
            .GetBySpecAsync(new ProjectSpecs.GetByName(Data.Data.TenantId, Data.Data.ProjectName.Value));
        if (project is null)
        {
            await Fail($"The project '{Data.Data.ProjectName}' does not exist.");
            return;
        }

        Data.Data.ProjectId = project.Id;

        Data.Data.ContentType = "application/yaml";
        Data.Data.OriginalConfig = CatletConfigYamlSerializer.Serialize(
            config.CloneWith(c => { c.Project = null; }));

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ContentType = Data.Data.ContentType,
            Configuration = Data.Data.OriginalConfig,
            Architecture = Data.Data.Architecture,
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
