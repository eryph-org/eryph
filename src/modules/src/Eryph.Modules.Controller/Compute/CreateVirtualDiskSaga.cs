using System;
using System.Threading.Tasks;
using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources.Disks;
using Eryph.ModuleCore;
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
    ISiteResolver siteResolver,
    IInventoryLockManager lockManager)
    : OperationTaskWorkflowSaga<CreateVirtualDiskCommand, EryphSagaData<CreateVirtualDiskSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<CreateVirtualDiskVMCommand>>
{
    public Task Handle(OperationTaskStatusEvent<CreateVirtualDiskVMCommand> message)
    {
        return FailOrRun(message, async (CreateVirtualDiskVMCommandResponse response) =>
        {
            var diskInfo = response.DiskInfo ?? throw new InvalidOperationException("DiskInfo is required.");

            await lockManager.AcquireVhdLock(diskInfo.DiskIdentifier);

            if (diskInfo.Name is null || diskInfo.DataStore is null
                || diskInfo.Environment is null)
                throw new InvalidOperationException(
                    $"The created virtual disk {diskInfo.DiskIdentifier} is missing the "
                    + "name, data store, or environment.");

            await stateStore.For<VirtualDisk>().AddAsync(new VirtualDisk
            {
                ProjectId = Data.Data.ProjectId,
                Id = Data.Data.DiskId,
                Name = diskInfo.Name,
                DataStore = diskInfo.DataStore,
                Environment = diskInfo.Environment,
                SiteId = Data.Data.SiteId,
                FileName = diskInfo.FileName,
                Path = diskInfo.Path,
                SizeBytes = diskInfo.SizeBytes,
                UsedSizeBytes = diskInfo.UsedSizeBytes,
                DiskIdentifier = diskInfo.DiskIdentifier,
                StorageIdentifier = diskInfo.StorageIdentifier,
            });

            await Complete();
        });
    }

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

        // A disk is created, so its environment decides its site. Resolve it once here and pin it on
        // the saga: the agent command is routed to that site, and the disk row records it.
        var environment = Optional(message.Environment).Filter(notEmpty).Match(
            EnvironmentName.New,
            () => EnvironmentName.New(EryphConstants.DefaultEnvironmentName));
        var site = await siteResolver.ResolveSite(environment);
        if (site.IsLeft)
        {
            await Fail(site.LeftToSeq().Head.Message);
            return;
        }

        Data.Data.SiteId = site.RightToSeq().Head;

        var result = CreateAgentCommand(message, project, Data.Data.SiteId);
        if (result.IsLeft)
        {
            await Fail(Error.Many(result.LeftToSeq()).Print());
            return;
        }

        var agentCommand = result.ValueUnsafe();
        Data.Data.AgentName = agentCommand.AgentName;
        await StartNewTask(agentCommand);
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateVirtualDiskSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<CreateVirtualDiskVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private Either<Error, CreateVirtualDiskVMCommand> CreateAgentCommand(
        CreateVirtualDiskCommand command,
        Project project,
        Guid siteId) =>
        from diskName in CatletDriveName.NewEither(command.Name)
        from storageIdentifier in StorageIdentifier.NewEither(command.StorageIdentifier)
        from dataStoreName in Optional(command.DataStore)
            .Filter(notEmpty)
            .Match(
                DataStoreName.NewEither,
                () => DataStoreName.New(EryphConstants.DefaultDataStoreName))
        from environmentName in Optional(command.Environment)
            .Filter(notEmpty)
            .Match(
                EnvironmentName.NewEither,
                () => EnvironmentName.New(EryphConstants.DefaultEnvironmentName))
        let projectName = ProjectName.New(project.Name)
        from agentName in agentLocator.FindAgentForDataStore(dataStoreName.Value, siteId)
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
