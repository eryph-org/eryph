using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Messages.Resources.Commands;
using Eryph.Modules.Controller.Inventory;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Projects;

[UsedImplicitly]
internal class DestroyProjectSaga(
    ILogger logger,
    IInventoryLockManager lockManager,
    IWorkflow workflow,
    IStateStore stateStore)
    : OperationTaskWorkflowSaga<DestroyProjectCommand, DestroyProjectSagaData>(workflow),
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
{
    protected override void CorrelateMessages(
        ICorrelationConfig<DestroyProjectSagaData> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    protected override async Task Initiated(DestroyProjectCommand message)
    {
        Data.ProjectId = message.ProjectId;

        if (Data.ProjectId == EryphConstants.DefaultProjectId)
        {
            await Fail(new ErrorData { ErrorMessage = "Default project cannot be deleted" });
            return;
        }

        var project = await stateStore.For<Project>().GetByIdAsync(Data.ProjectId);
        if (project == null)
        {
            await Complete();
            return;
        }

        await stateStore.LoadCollectionAsync(project, x => x.Resources);
        if (project.Resources.Count == 0)
        {
            await DeleteProject();
            await Complete();
            return;
        }

        project.BeingDeleted = true;

        await StartNewTask(new DestroyResourcesCommand
        {
            Resources = project.Resources.Select(x=> new Resource(x.ResourceType, x.Id)).ToArray()
        });
    }

    private async Task DeleteProject()
    {
        var project = await stateStore.For<Project>().GetByIdAsync(Data.ProjectId);
        if (project is null)
            return;

        await stateStore.LoadCollectionAsync(project, p => p.Resources);

        logger.LogInformation(
            "Deleting project {ProjectId} {ProjectName} with following resources still present:\n{Resources}",
            project.Id, project.Name, string.Join("\n", project.Resources.ToSeq()
                .Map(r => $"{r.ResourceType} {r.Name} {(r is VirtualDisk d ? $"Deleted: {d.Deleted}" : "")}")));

        var disks = await stateStore.For<VirtualDisk>()
            .ListAsync(new VirtualDiskSpecs.FindDeletedInProject(project.Id));
        var diskIdentifiers = disks.Select(d => d.DiskIdentifier).Distinct().Order();
        foreach (var diskIdentifier in diskIdentifiers)
        {
            await lockManager.AcquireVhdLock(diskIdentifier);
        }
        await stateStore.For<VirtualDisk>().DeleteRangeAsync(disks);

        var roleAssignments = await stateStore.For<ProjectRoleAssignment>()
            .ListAsync(new ProjectRoleAssignmentSpecs.GetByProject(project.Id));
        await stateStore.For<ProjectRoleAssignment>().DeleteRangeAsync(roleAssignments);
        
        await stateStore.For<Project>().DeleteAsync(project);
    }

    public Task Handle(OperationTaskStatusEvent<DestroyResourcesCommand> message)
    {
        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            await DeleteProject();
            await Complete(response);
        });
    }
}
