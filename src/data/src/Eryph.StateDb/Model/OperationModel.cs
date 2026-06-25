using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class OperationModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public List<OperationLogEntry> LogEntries { get; set; } = null!;

    public List<OperationTaskModel> Tasks { get; set; } = null!;

    public List<OperationResourceModel> Resources { get; set; } = null!;

    public List<OperationProjectModel> Projects { get; set; } = null!;

    public OperationStatus Status { get; set; }

    public string? StatusMessage { get; set; }

    /// <summary>
    /// The id of the user who requested the operation. Null for operations
    /// started by the system (e.g. inventory or event handlers).
    /// </summary>
    public string? RequestedBy { get; set; }

    /// <summary>
    /// When the operation was created (queued).
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// When the operation first started running. Null while still queued.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the operation reached a terminal state (completed, failed or cancelled).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset LastUpdated { get; set; }

    public string? ResultData { get; set; }

    public string? ResultType { get; set; }
}
