using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class Operation
    {
        public Guid Id { get; set; }
        public virtual List<OperationLogEntry> LogEntries { get; set; }
        public virtual List<OperationTask> Tasks { get; set; }
        public virtual List<OperationResource> Resources { get; set; }
        public OperationStatus Status { get; set; }

        public string StatusMessage { get; set; }
    }
}