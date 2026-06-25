using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class OperationTaskModel
{
    public Guid Id { get; set; }

    public Guid ParentTaskId { get; set; }

    public Guid OperationId { get; set; }

    public OperationModel Operation { get; set; } = null!;

    public OperationTaskStatus Status { get; set; }

    public string? AgentName { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public TaskReferenceType? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? ReferenceProjectName { get; set; }

    /// <summary>
    /// When the task was created (queued).
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// When the task first started running. Null while still queued.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the task reached a terminal state (completed or failed).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset LastUpdated { get; set; }

    public List<TaskProgressEntry> Progress { get; set; } = null!;
}
