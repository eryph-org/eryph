using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using IdGen;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSaga(
    IWorkflow workflow,
    IBus bus,
    IIdGenerator<long> idGenerator,
    IInventoryLockManager lockManager,
    IVirtualMachineDataService vmDataService,
    IStateStore stateStore)
    : OperationTaskWorkflowSaga<CreateCatletCommand, EryphSagaData<CreateCatletSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>,
        IHandleMessages<OperationTaskStatusEvent<ExpandFodderVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CreateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>
{
    protected override async Task Initiated(CreateCatletCommand message)
    {
        Data.Data.State = CreateCatletSagaState.Initiated;
        Data.Data.Config = message.Config;
        Data.Data.TenantId = message.TenantId;

        Data.Data.MachineId = Guid.NewGuid();

        await StartNewTask(new PrepareNewCatletConfigCommand
        {
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareNewCatletConfigCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.ConfigPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareNewCatletConfigCommandResponse response) =>
        {
            Data.Data.State = CreateCatletSagaState.ConfigPrepared;
            
            Data.Data.AgentName = response.AgentName;
            Data.Data.Architecture = response.Architecture;

            Data.Data.Config = response.ResolvedConfig;
            Data.Data.BredConfig = response.BredConfig;
            Data.Data.ParentConfig = response.ParentConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            Data.Data.PendingGenes = response.ResolvedGenes;

            if (Data.Data.PendingGenes.Count == 0)
            {
                await StartExpandFodder();
                return;
            }

            var commands = Data.Data.PendingGenes.Map(id => new PrepareGeneCommand
            {
                AgentName = Data.Data.AgentName,
                Gene = id,
            });

            foreach (var command in commands)
            {
                await StartNewTask(command);
            }
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.GenesPrepared)
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

            await StartExpandFodder();
        });
    }

    public Task Handle(OperationTaskStatusEvent<ExpandFodderVMCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.FodderExpanded)
            return Task.CompletedTask;

        // The expand command is only triggered to ensure that the fodder and
        // variables are valid. We do not need the result as this point.
        return FailOrRun(message, async (ExpandFodderVMCommandResponse _) =>
        {
            Data.Data.State = CreateCatletSagaState.FodderExpanded;

            await StartNewTask(new CreateCatletVMCommand
            {
                Config = Data.Data.BredConfig,
                NewMachineId = Data.Data.MachineId,
                AgentName = Data.Data.AgentName,
                StorageId = idGenerator.CreateId()
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<CreateCatletVMCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.Created)
            return Task.CompletedTask;

        return FailOrRun(message, async (ConvergeCatletResult response) =>
        {
            await lockManager.AcquireVmLock(response.VmId);
            Data.Data.State = CreateCatletSagaState.Created;

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

            var catletMetadata = new Resources.Machines.CatletMetadata
            {
                Id = response.MetadataId,
                MachineId = Data.Data.MachineId,
                VMId = response.VmId,
                Fodder = Data.Data.Config!.Fodder,
                Variables = Data.Data.Config.Variables,
                Parent = Data.Data.Config.Parent,
                ParentConfig = Data.Data.ParentConfig,
                Architecture = Data.Data.Architecture!.Value,
                ResolvedFodderGenes = Data.Data.ResolvedGenes
                    .Filter(g => g.GeneType is GeneType.Fodder)
                    .ToDictionary(
                        ugi => ugi.Id.Value,
                        ugi => ugi.Architecture.Value),
            };

            _ = await vmDataService.AddNewVM(new Catlet
            {
                ProjectId = project.Id,
                Id = Data.Data.MachineId,
                AgentName = Data.Data.AgentName,
                VMId = response.Inventory.VMId,
                Name = response.Inventory.Name,
                Environment = environmentName.Value,
                DataStore = datastoreName.Value,
                // Ensure that any inventory updates are applied as the
                // information which we save right now is incomplete.
                LastSeen = DateTimeOffset.MinValue,
                LastSeenState = DateTimeOffset.MinValue,
            }, catletMetadata);

            await StartNewTask(new UpdateCatletCommand
            {
                Config = Data.Data.Config,
                BredConfig = Data.Data.BredConfig,
                CatletId = Data.Data.MachineId,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletCommand> message)
    {
        if (Data.Data.State >= CreateCatletSagaState.Updated)
            return Task.CompletedTask;

        return FailOrRun(message, () =>
        {
            Data.Data.State = CreateCatletSagaState.Updated;
            return Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateCatletSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<ExpandFodderVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<CreateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task StartExpandFodder()
    {
        Data.Data.State = CreateCatletSagaState.GenesPrepared;

        await StartNewTask(new ExpandFodderVMCommand
        {
            AgentName = Data.Data.AgentName,
            Config = Data.Data.BredConfig,
            ResolvedGenes = Data.Data.ResolvedGenes,
        });
    }
}
