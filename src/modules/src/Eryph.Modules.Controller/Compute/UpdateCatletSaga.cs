using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Yaml;
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
using LanguageExt.UnsafeValueAccess;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DeployCatletCommand>>
{
    protected override async Task Initiated(UpdateCatletCommand message)
    {
        Data.Data.State = UpdateCatletSagaState.Initiated;
        Data.Data.Config = message.Config;
        Data.Data.CatletId = message.CatletId;

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

        await StartNewTask(new ResolveCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = CatletConfigYamlSerializer.Serialize(message.Config),
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= UpdateCatletSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = UpdateCatletSagaState.SpecificationBuilt;

            // TODO merge built config with original config

            Data.Data.BuiltConfig = response.BuiltConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            await StartNewTask(new ValidateCatletDeploymentCommand
            {
                TenantId = Data.Data.TenantId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
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
                TenantId = Data.Data.TenantId,
                AgentName = Data.Data.AgentName,
                Architecture = Data.Data.Architecture,
                Config = Data.Data.BuiltConfig,
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

        config.Correlate<OperationTaskStatusEvent<ResolveCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ValidateCatletDeploymentCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DeployCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Task<Option<(Catlet Catlet, CatletMetadata Metadata)>> GetCatletMetadata(Guid catletId) =>
        from catlet in vmDataService.GetVM(catletId)
        from metadata in metadataService.GetMetadata(catlet.MetadataId)
        select (catlet, metadata);
}
