using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using IdGen;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSaga(
    IStorageIdentifierGenerator storageIdentifierGenerator,
    IStateStore stateStore,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CreateCatletCommand, EryphSagaData<CreateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DeployCatletCommand>>
{
    protected override async Task Initiated(CreateCatletCommand message)
    {
        Data.Data.State = CreateCatletSagaState.Initiated;
        Data.Data.TenantId = message.TenantId;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);
        Data.Data.ConfigYaml = message.ConfigYaml;

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = message.ConfigYaml,
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = CreateCatletSagaState.SpecificationBuilt;

            Data.Data.ResolvedGenes = response.ResolvedGenes;

            var project = await stateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(Data.Data.TenantId, response.BuiltConfig.Project!));
            if (project is null)
            {
                await Fail($"The project '{response.BuiltConfig.Project}' does not exist");
                return;
            }

            Data.Data.ProjectId = project.Id;
            Data.Data.BuiltConfig = CatletConfigInstantiator.Instantiate(
                response.BuiltConfig, storageIdentifierGenerator.Generate());

            await StartNewTask(new ValidateCatletDeploymentCommand
            {
                TenantId = Data.Data.TenantId,
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
        if (Data.Data.State >= CreateCatletSagaState.DeploymentValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async () =>
        {
            Data.Data.State = CreateCatletSagaState.DeploymentValidated;

            await StartNewTask(new DeployCatletCommand
            {
                TenantId = Data.Data.TenantId,
                ProjectId = Data.Data.ProjectId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                ConfigYaml = Data.Data.ConfigYaml,
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
