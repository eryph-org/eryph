using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using ErrorData = Dbosoft.Rebus.Operations.ErrorData;
using OperationStatus = Eryph.StateDb.Model.OperationStatus;

namespace Eryph.StateDb.Workflows;

public class OperationManager : OperationManagerBase
{
    private readonly IBus _bus;
    private readonly ILogger<OperationManager> _log;
    private readonly StateStoreContext _db;

    public OperationManager(StateStoreContext db, IBus bus, ILogger<OperationManager> log)
    {
        _bus = bus;
        _log = log;
        _db = db;
    }

    public override async ValueTask<IOperation?> GetByIdAsync(Guid operationId)
    {
        var res = await _db.Operations.FindAsync(operationId);
        return res == null ? null : new Operation(res);
    }

    public override async ValueTask<IOperation> GetOrCreateAsync(Guid operationId, object command, [CanBeNull] object additionalData, 
        IDictionary<string,string> additionalHeaders)
    {
        if (additionalData is not OperationDataRecord dataRecord)
            throw new InvalidOperationException(
                $"additional data of type {nameof(OperationDataRecord)} is required to create a operation");

        var res = await _db.Operations.FindAsync(operationId);
        if (res != null) return new Operation(res);

        var (resources, projects) = await OperationsHelper.GetCommandProjectsAndResources(command, _db);
        
        res = new OperationModel
        {
            Id = operationId,
            Status = OperationStatus.Queued,
            TenantId = dataRecord.TenantId,
            StatusMessage = Dbosoft.Rebus.Operations.OperationStatus.Queued.ToString(),
            Resources = resources,
            Projects = projects
        };

        await _db.AddAsync(res);
        return new Operation(res);
    }


    public override async ValueTask AddProgressAsync(Guid progressId, DateTimeOffset timestamp, IOperation operation,
        IOperationTask task,
        object? data, IDictionary<string, string> messageHeaders)
    {
        _log.LogDebug("Received operation task progress event. Id : '{operationId}/{taskId}'", operation.Id, task.Id);

        var message = "";
        var progress = 0;

        switch (data)
        {
            case JsonElement { ValueKind: JsonValueKind.Object } dataElement:
                message = dataElement.GetProperty("message").GetString();
                progress = dataElement.GetProperty("progress").GetInt32();
                break;
            case JsonElement { ValueKind: JsonValueKind.String } stringElement:
                message = stringElement.GetString();
                break;
            default:
                _log.LogDebug("Invalid operation task progress event: data has to be a json object or a string. Id : '{operationId}/{taskId}'",
                    operation.Id, task.Id);
                break;
        }


        var taskEntry = await _db.OperationTasks.FindAsync(task.Id);
        if (taskEntry != null)
        {
            taskEntry.Progress = progress;
        }

        var opLogEntry =
            new OperationLogEntry
            {
                Id = progressId,
                OperationId = operation.Id,
                TaskId = task.Id,
                Message = message,
                Timestamp = timestamp
            };

        await _db.Logs.AddAsync(opLogEntry).ConfigureAwait(false);

        
    }

    public override async ValueTask<bool> TryChangeStatusAsync(IOperation operation,
        Dbosoft.Rebus.Operations.OperationStatus newStatus, object? additionalData, IDictionary<string, string> messageHeaders)
    {
        if (operation is not Operation op) return false;

        op.Model.Status = newStatus switch
        {
            Dbosoft.Rebus.Operations.OperationStatus.Queued => OperationStatus.Queued,
            Dbosoft.Rebus.Operations.OperationStatus.Running => OperationStatus.Running,
            Dbosoft.Rebus.Operations.OperationStatus.Failed => OperationStatus.Failed,
            Dbosoft.Rebus.Operations.OperationStatus.Completed => OperationStatus.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, null)
        };

        op.Model.StatusMessage = additionalData switch
        {
            ErrorData errorData => errorData.ErrorMessage,
            string errorMessage => errorMessage,
            _ => op.Model.Status.ToString()
        };

        // make sure that just created projects are added to the operation
        if (additionalData is ProjectReference projectReference)
        { 
            await _db.Entry(op.Model).Collection(x => x.Projects).LoadAsync();
            if(op.Model.Projects.All(x => x.ProjectId != projectReference.ProjectId))
                op.Model.Projects.Add(new OperationProjectModel
                {
                    ProjectId = projectReference.ProjectId,
                });
        }

        return true;
    }
}