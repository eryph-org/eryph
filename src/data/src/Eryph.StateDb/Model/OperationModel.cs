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

    public DateTimeOffset LastUpdated { get; set; }

    public string? ResultData { get; set; }

    public string? ResultType { get; set; }
}
