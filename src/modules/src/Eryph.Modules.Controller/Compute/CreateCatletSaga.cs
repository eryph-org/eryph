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
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>,
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

        await StartNewTask(new ResolveCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = message.ConfigYaml,
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = CreateCatletSagaState.SpecificationBuilt;

            Data.Data.ResolvedGenes = response.ResolvedGenes;

            var project = await stateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(Data.Data.TenantId, response.BuiltConfig.Project!));
            if (project is null)
            {
                await Fail($"The project '{Data.Data.BuiltConfig.Project}' does not exist");
                return;
            }

            Data.Data.ProjectId = project.Id;

            var storageIdentifier = storageIdentifierGenerator.Generate();
            Data.Data.BuiltConfig = GenerateMacAddresses(ApplyStorageIdentifier(
                response.BuiltConfig, storageIdentifier));

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

        config.Correlate<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private CatletConfig ApplyStorageIdentifier(
        CatletConfig catletConfig,
        string storageIdentifier) =>
        catletConfig.CloneWith(c =>
        {
            c.Location = Optional(c.Location).Filter(notEmpty).IfNone(storageIdentifier);
            c.Drives = c.Drives.ToSeq()
                .Map(d => ApplyStorageIdentifier(d, c.Location))
                .ToArray();
        });

    private CatletDriveConfig ApplyStorageIdentifier(
        CatletDriveConfig driveConfig,
        string storageIdentifier) =>
        driveConfig.CloneWith(d =>
        {
            d.Location = Optional(d.Location).Filter(notEmpty).IfNone(storageIdentifier);
        });

    private CatletConfig GenerateMacAddresses(
        CatletConfig catletConfig) =>
        catletConfig.CloneWith(c =>
        {
            c.NetworkAdapters = c.NetworkAdapters.ToSeq()
                .Map(GenerateMacAddress)
                .ToArray();
        });

    private CatletNetworkAdapterConfig GenerateMacAddress(
        CatletNetworkAdapterConfig adapterConfig) =>
        adapterConfig.CloneWith(a =>
        {
            a.MacAddress = Optional(a.MacAddress).Filter(notEmpty)
                .IfNone(() => MacAddressGenerator.Generate().Value);
        });
}
