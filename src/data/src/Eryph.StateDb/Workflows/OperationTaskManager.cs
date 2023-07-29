using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using OperationTaskStatus = Dbosoft.Rebus.Operations.OperationTaskStatus;
using Resource = Eryph.Resources.Resource;

namespace Eryph.StateDb.Workflows;
#nullable enable

public class OperationTaskManager : OperationTaskManagerBase
{
    private readonly StateStoreContext _db;

    public OperationTaskManager(StateStoreContext db)
    {
        _db = db;
    }


    public override async ValueTask<IOperationTask?> GetByIdAsync(Guid taskId)
    {
        var res = await _db.OperationTasks.FindAsync(taskId);
        return res == null ? null : new OperationTask(res);
    }

    public override async ValueTask<IOperationTask> GetOrCreateAsync(IOperation operation, object command, Guid taskId,
        Guid parentTaskId)
    {
        var res = await _db.OperationTasks.FindAsync(taskId);
        if (res != null) return new OperationTask(res);

        var (resources, projects) = await OperationsHelper.GetCommandProjectsAndResources(command, _db);

        res = new OperationTaskModel
        {
            Id = taskId,
            OperationId = operation.Id,
            Status = Model.OperationTaskStatus.Queued,
            ParentTaskId = parentTaskId,
            Name = command.GetType().Name
        };

        await _db.AddAsync(res);

        var operationModel = await _db.Operations.Where(x => x.Id == operation.Id)
            .Include(x => x.Projects)
            .Include(x => x.Resources).FirstOrDefaultAsync();

        if (operationModel == null) return new OperationTask(res);

        var newResources = resources.ExceptBy(operationModel.Resources.Select(
            x => new Resource(x.ResourceType, x.ResourceId)), x => new Resource(x.ResourceType, x.ResourceId));

        var newProjects = projects.ExceptBy(operationModel.Projects.Select(
            x => x.ProjectId), x => x.ProjectId);

        operationModel.Resources.AddRange(newResources);
        operationModel.Projects.AddRange(newProjects);


        return new OperationTask(res);
    }

    public override ValueTask<bool> TryChangeStatusAsync(IOperationTask task, OperationTaskStatus newStatus,
        object? additionalData)
    {
        if (task is not OperationTask opTask) return new ValueTask<bool>(false);

        opTask.Model.Status = newStatus switch
        {
            OperationTaskStatus.Queued => Model.OperationTaskStatus.Queued,
            OperationTaskStatus.Running => Model.OperationTaskStatus.Running,
            OperationTaskStatus.Failed => Model.OperationTaskStatus.Failed,
            OperationTaskStatus.Completed => Model.OperationTaskStatus.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, null)
        };

        return new ValueTask<bool>(true);
    }
}