using System;

namespace Eryph.StateDb.Model;

public class OperationLogEntry
{
    public Guid Id { get; set; }

    public Guid OperationId { get; set; }

    public virtual OperationModel Operation { get; set; } = null!;

    public Guid TaskId { get; set; }

    public virtual OperationTaskModel Task { get; set; } = null!;

    public string? Message { get; set; }
        
    public DateTimeOffset Timestamp { get; set; }
}
