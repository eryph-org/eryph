using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
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
        IHandleMessages<OperationTaskStatusEvent<CreateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletCommand>>
{
    protected override async Task Initiated(CreateCatletCommand message)
    {
        Data.Data.State = CreateVMState.Initiated;
        Data.Data.Config = message.Config;
        Data.Data.TenantId = message.TenantId;
        
        await StartNewTask(new PrepareNewCatletConfigCommand
        {
            Config = message.Config,
            TenantId = message.TenantId,
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareNewCatletConfigCommand> message)
    {
        if (Data.Data.State >= CreateVMState.ConfigPrepared)
            return Task.CompletedTask;

        return FailOrRun(message, async (PrepareNewCatletConfigCommandResponse response) =>
        {
            Data.Data.State = CreateVMState.ConfigPrepared;
            
            Data.Data.AgentName = response.AgentName;
            Data.Data.Architecture = response.Architecture;

            Data.Data.Config = response.Config;
            Data.Data.BredConfig = response.BredConfig;
            Data.Data.ParentConfig = response.ParentConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

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
            await lockManager.AcquireVmLock(response.VmId);
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

        config.Correlate<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<CreateCatletVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
