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
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateVirtualDiskSaga(
    IWorkflow workflow,
    IStateStore stateStore,
    IStorageManagementAgentLocator agentLocator,
    IVirtualDiskDataService dataService)
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

        Data.Data.AgentName = agentLocator.FindAgentForDataStore(
            message.DataStore ?? EryphConstants.DefaultDataStoreName,
            message.Environment ?? EryphConstants.DefaultEnvironmentName);

        var environmentName = Optional(message.Environment).Filter(notEmpty).Match(
            Some: n => EnvironmentName.New(n),
            None: () => EnvironmentName.New("default"));

        var datastoreName = Optional(message.DataStore).Filter(notEmpty).Match(
            Some: n => DataStoreName.New(n),
            None: () => DataStoreName.New("default"));

        await StartNewTask(new CreateVirtualDiskVMCommand
        {
            AgentName = Data.Data.AgentName,
            ProjectName = ProjectName.New(project.Name), 
            DataStore = datastoreName,
            Environment = environmentName,
            Name = CatletDriveName.New(message.Name),
            DiskId = Data.Data.DiskId,
            Size = message.Size,
            StorageIdentifier = StorageIdentifier.New(message.StorageIdentifier),
        });
    }

    public Task Handle(OperationTaskStatusEvent<CreateVirtualDiskVMCommand> message)
    {
        return FailOrRun(message, async (CreateVirtualDiskVMCommandResponse response) =>
        {
            await dataService.AddNewVHD(new VirtualDisk()
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
}
