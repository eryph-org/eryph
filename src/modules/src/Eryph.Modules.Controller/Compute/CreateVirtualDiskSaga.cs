using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Disks;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.Inventory;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateVirtualDiskSaga(
    IWorkflow workflow,
    IStateStore stateStore,
    IStorageManagementAgentLocator agentLocator,
    IInventoryLockManager lockManager)
    : OperationTaskWorkflowSaga<CreateVirtualDiskCommand, EryphSagaData<CreateVirtualDiskSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<CreateVirtualDiskVMCommand>>
{
    protected override async Task Initiated(CreateVirtualDiskCommand message)
    {
        Data.Data.DiskId = Guid.NewGuid();
        Data.Data.ProjectId = message.ProjectId;

        var project = await stateStore.For<Project>().GetByIdAsync(Data.Data.ProjectId);
        if (project is null)
        {
            await Fail($"The project {Data.Data.ProjectId} does not exist");
            return;
        }

        var result = CreateAgentCommand(message, project);
        if (result.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(result.LeftToSeq())));
            return;
        }

        var agentCommand = result.ValueUnsafe();
        Data.Data.AgentName = agentCommand.AgentName;
        await StartNewTask(agentCommand);
    }

    public Task Handle(OperationTaskStatusEvent<CreateVirtualDiskVMCommand> message)
    {
        return FailOrRun(message, async (CreateVirtualDiskVMCommandResponse response) =>
        {
            await lockManager.AcquireVhdLock(response.DiskInfo.DiskIdentifier);

            await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
            {
                ProjectId = Data.Data.ProjectId,
                Id = Data.Data.DiskId,
                Name = response.DiskInfo.Name,
                DataStore = response.DiskInfo.DataStore,
                Environment = response.DiskInfo.Environment,
                FileName = response.DiskInfo.FileName,
                Path = response.DiskInfo.Path,
                SizeBytes = response.DiskInfo.SizeBytes,
                UsedSizeBytes = response.DiskInfo.UsedSizeBytes,
                DiskIdentifier = response.DiskInfo.DiskIdentifier,
                StorageIdentifier = response.DiskInfo.StorageIdentifier,
            });

            await Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateVirtualDiskSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<CreateVirtualDiskVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Either<Error, CreateVirtualDiskVMCommand> CreateAgentCommand(
        CreateVirtualDiskCommand command,
        Project project) =>
        from diskName in CatletDriveName.NewEither(command.Name)
        from storageIdentifier in StorageIdentifier.NewEither(command.StorageIdentifier)
        from dataStoreName in Optional(command.DataStore)
            .Filter(notEmpty)
            .Match(
                Some: DataStoreName.NewEither,
                None: () => DataStoreName.New(EryphConstants.DefaultDataStoreName))
        from environmentName in Optional(command.Environment)
            .Filter(notEmpty)
            .Match(
                Some: EnvironmentName.NewEither,
                None: () => EnvironmentName.New(EryphConstants.DefaultEnvironmentName))
        let projectName = ProjectName.New(project.Name)
        let agentName = agentLocator.FindAgentForDataStore(
            dataStoreName.Value, environmentName.Value)
        select new CreateVirtualDiskVMCommand
        {
            AgentName = agentName,
            ProjectName = projectName,
            DataStore = dataStoreName,
            Environment = environmentName,
            Name = diskName,
            DiskId = Data.Data.DiskId,
            Size = command.Size,
            StorageIdentifier = storageIdentifier,
        };
}
