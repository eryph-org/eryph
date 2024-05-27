using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class OperationModel
{
    public Guid Id { get; set; }
    
    public Guid TenantId { get; set; }

    public virtual List<OperationLogEntry> LogEntries { get; set; } = null!;
    
    public virtual List<OperationTaskModel> Tasks { get; set; } = null!;
    
    public virtual List<OperationResourceModel> Resources { get; set; } = null!;

    public virtual List<OperationProjectModel> Projects { get; set; } = null!;

    public OperationStatus Status { get; set; }

    public string? StatusMessage { get; set; }

    public DateTimeOffset LastUpdated { get; set; }
}
