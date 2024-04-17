using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eryph.StateDb.Model;

public class OperationModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public virtual List<OperationLogEntry> LogEntries { get; set; }
    public virtual List<OperationTaskModel> Tasks { get; set; }
    public virtual List<OperationResourceModel> Resources { get; set; }

    public virtual List<OperationProjectModel> Projects { get; set; }


    public OperationStatus Status { get; set; }

    public string StatusMessage { get; set; }
    public DateTimeOffset LastUpdated { get; set; }


}