using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OperationTaskStatus = Dbosoft.Rebus.Operations.OperationTaskStatus;
using Resource = Eryph.Resources.Resource;

namespace Eryph.StateDb.Workflows;
#nullable enable

public class OperationTaskManager : OperationTaskManagerBase
{
    private readonly StateStoreContext _db;
    private readonly ILogger _logger;

    public OperationTaskManager(StateStoreContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }


    public override async ValueTask<IOperationTask?> GetByIdAsync(Guid taskId)
    {
        var res = await _db.OperationTasks.FindAsync(taskId);
        return res == null ? null : new OperationTask(res);
    }

    public override async ValueTask<IOperationTask> GetOrCreateAsync(IOperation operation, 
        object command, DateTimeOffset timestamp,
        Guid taskId, Guid parentTaskId)
    {
        _logger.LogTrace("Entering GetOrCreateAsync for operation {operationId} and task {taskId}  with parent {parentTaskId}", 
            operation.Id, taskId, parentTaskId);
        var res = await _db.OperationTasks.FindAsync(taskId);
        if (res != null)
            return new OperationTask(res);

        var (resources, projects) = await OperationsHelper.GetCommandProjectsAndResources(command, _db);

        string? displayName = null;

        if(command is ICommandWithName commandWithName)
            displayName = commandWithName.GetCommandName();

        res = new OperationTaskModel
        {
            Id = taskId,
            OperationId = operation.Id,
            Status = Model.OperationTaskStatus.Queued,
            ParentTaskId = parentTaskId,
            Name = command.GetType().Name,
            DisplayName = displayName,
            LastUpdated = timestamp
        };
        _logger.LogDebug("Creating task {taskId} for operation {operationId} with parent {parentTaskId}", taskId, operation.Id, parentTaskId);
        _logger.LogTrace("Created task: {@task}", res);

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


    public override ValueTask<bool> TryChangeStatusAsync(IOperationTask task, 
        OperationTaskStatus newStatus,
        DateTimeOffset timestamp, object? additionalData)
    {
        _logger.LogTrace(
            "Entering TryChangeStatusAsync for operation {operationId} and task {taskId}. New status: {newStatus}",
            task.OperationId, task.Id, newStatus);

        if (task is not OperationTask opTask) return new ValueTask<bool>(false);

        if (opTask.Model.LastUpdated > timestamp)
        {
            _logger.LogWarning("Operation: {operationId}, Task {taskId}: has been updated already after change timestamp. Skipping status change. Task last changed: {lastChanged}, change timestamp: {timestamp}", 
                task.OperationId, task.Id, opTask.Model.LastUpdated, timestamp);
            return new ValueTask<bool>(false);

        }

        if (opTask.Status is OperationTaskStatus.Completed or OperationTaskStatus.Failed)
        {
            _logger.LogWarning("Operation: {operationId}, Task {taskId}: has already been completed or failed. Skipping status change. Task status: {status}", 
                               task.OperationId, task.Id, opTask.Status);
            return new ValueTask<bool>(false);
        }

        opTask.Model.Status = newStatus switch
        {
            OperationTaskStatus.Queued => Model.OperationTaskStatus.Queued,
            OperationTaskStatus.Running => Model.OperationTaskStatus.Running,
            OperationTaskStatus.Failed => Model.OperationTaskStatus.Failed,
            OperationTaskStatus.Completed => Model.OperationTaskStatus.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, null)
        };

        if (additionalData is ITaskReference taskReference)
        {
            opTask.Model.ReferenceType = taskReference.ReferenceType;
            opTask.Model.ReferenceId = taskReference.ReferenceId;
            opTask.Model.ReferenceProjectName = taskReference.ProjectName;
        }

        opTask.Model.LastUpdated = timestamp;

        _logger.LogTrace("Operation: {operationId}, Task {taskId}: updated task model : {@taskModel}",
            task.OperationId, task.Id, opTask.Model);


        return new ValueTask<bool>(true);
    }
}