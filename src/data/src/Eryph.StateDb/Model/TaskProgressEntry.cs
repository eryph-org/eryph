using System;

namespace Eryph.StateDb.Model;

public class TaskProgressEntry
{
    public Guid Id { get; set; }

    public Guid OperationId { get; set; }
    
    public Guid TaskId { get; set; }

    public OperationTaskModel Task { get; set; } = null!;
    
    public DateTimeOffset Timestamp { get; set; }
    
    public int Progress { get; set; }
}