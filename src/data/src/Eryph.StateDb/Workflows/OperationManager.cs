using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
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

        if (data is not JsonElement { ValueKind: JsonValueKind.String } messageElement)
        {
            _log.LogDebug("Invalid operation task progress event: data has to be a message string. Id : '{operationId}/{taskId}'", 
                operation.Id, task.Id);

            return;
        }

        var opLogEntry =
            new OperationLogEntry
            {
                Id = progressId,
                OperationId = operation.Id,
                TaskId = task.Id,
                Message = messageElement.GetString(),
                Timestamp = timestamp
            };

        await _db.Logs.AddAsync(opLogEntry).ConfigureAwait(false);
    }

    public override ValueTask<bool> TryChangeStatusAsync(IOperation operation,
        Dbosoft.Rebus.Operations.OperationStatus newStatus, object? additionalData, IDictionary<string, string> messageHeaders)
    {
        if (operation is not Operation op) return new ValueTask<bool>(false);

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


        return new ValueTask<bool>(true);
    }
}