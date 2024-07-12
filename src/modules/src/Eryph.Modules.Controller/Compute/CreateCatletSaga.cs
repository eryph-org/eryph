using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using IdGen;
using JetBrains.Annotations;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSaga(
    IWorkflow workflow,
    IIdGenerator<long> idGenerator,
    IVirtualMachineDataService vmDataService,
    IStateStore stateStore)
    : OperationTaskWorkflowSaga<CreateCatletCommand, EryphSagaData<CreateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PlaceCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ResolveCatletConfigCommand>>
{
    protected override Task Initiated(CreateCatletCommand message)
    {
        Data.Data.Config = message.Config;
        Data.Data.State = CreateVMState.Initiated;
        Data.Data.TenantId = message.TenantId;
        return StartNewTask(new ValidateCatletConfigCommand
            {
                MachineId = Guid.Empty,
                Config = message.Config
            }
        ).AsTask();
    }

    public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
    {
        if (Data.Data.State >= CreateVMState.ConfigValidated)
            return Task.CompletedTask;

        return FailOrRun(message, async (ValidateCatletConfigCommand response) =>
        {
            Data.Data.Config = response.Config;
            Data.Data.State = CreateVMState.ConfigValidated;

            await StartNewTask(new PlaceCatletCommand
            {
                Config = Data.Data.Config
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<PlaceCatletCommand> message)
    {
        if (Data.Data.State >= CreateVMState.Placed)
            return Task.CompletedTask;

        return FailOrRun(message, async (PlaceCatletResult response) =>
        {
            Data.Data.State = CreateVMState.Placed;
            Data.Data.AgentName = response.AgentName;

            await StartNewTask(new ResolveCatletConfigCommand()
            {
                AgentName = response.AgentName,
                Config = Data.Data.Config,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<ResolveCatletConfigCommand> message)
    {
        if (Data.Data.State >= CreateVMState.Resolved)
            return Task.CompletedTask;

        return FailOrRun(message, async (ResolveCatletConfigCommandResponse response) =>
        {
            Data.Data.State = CreateVMState.Resolved;

            var resolveResult = CatletGeneResolving.ResolveGenesetIdentifiers(
                Data.Data.Config,
                response.ResolvedGeneSets.ToHashMap());
            if (resolveResult.IsLeft)
            {
                await Fail(ErrorUtils.PrintError(Error.New("Could not resolve genes in catlet.",
                    Error.Many(resolveResult.LeftToSeq()))));
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

            Data.Data.Config = resolveResult.ValueUnsafe();
            Data.Data.BredConfig = breedingResult.ValueUnsafe().Config;
            Data.Data.ParentConfig = breedingResult.ValueUnsafe().ParentConfig.IfNoneUnsafe((CatletConfig?)null);
            Data.Data.MachineId = Guid.NewGuid();

            await StartNewTask(new CreateCatletVMCommand
            {
                Config = Data.Data.Config,
                BredConfig = Data.Data.BredConfig,
                NewMachineId = Data.Data.MachineId,
                AgentName = Data.Data.AgentName,
                StorageId = idGenerator.CreateId()
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<CreateCatletVMCommand> message)
    {
        if (Data.Data.State >= CreateVMState.Created)
            return Task.CompletedTask;

        return FailOrRun(message, async (ConvergeCatletResult response) =>
        {
            Data.Data.State = CreateVMState.Created;

            var projectName = Optional(Data.Data.Config?.Project).Filter(notEmpty).Match(
                Some: n => ProjectName.New(n),
                None: () => ProjectName.New("default"));

            var environmentName = Optional(Data.Data.Config?.Environment).Filter(notEmpty).Match(
                Some: n => EnvironmentName.New(n),
                None: () => EnvironmentName.New("default"));

            var datastoreName = Optional(Data.Data.Config?.Store).Filter(notEmpty).Match(
                Some: n => DataStoreName.New(n),
                None: () => DataStoreName.New("default"));

            var project = await stateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(Data.Data.TenantId, projectName.Value));

            if (project == null)
                throw new InvalidOperationException($"Project '{projectName}' not found.");

            response.MachineMetadata.ParentConfig = Data.Data.ParentConfig;

            _ = await vmDataService.AddNewVM(new Catlet
            {
                ProjectId = project.Id,
                Id = Data.Data.MachineId,
                AgentName = Data.Data.AgentName,
                VMId = response.Inventory.VMId,
                Name = response.Inventory.Name,
                Environment = environmentName.Value,
                DataStore = datastoreName.Value,
            }, response.MachineMetadata);

            await StartNewTask(new UpdateCatletCommand
            {
                Config = Data.Data.Config,
                BreedConfig = Data.Data.BredConfig,
                CatletId = Data.Data.MachineId
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletCommand> message)
    {
        if (Data.Data.State >= CreateVMState.Updated)
            return Task.CompletedTask;

        return FailOrRun(message, () =>
        {
            Data.Data.State = CreateVMState.Updated;
            return Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PlaceCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ResolveCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<CreateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
