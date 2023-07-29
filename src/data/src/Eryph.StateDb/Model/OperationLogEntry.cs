using System;
using Eryph.StateDb.Workflows;

namespace Eryph.StateDb.Model
{
    public class OperationLogEntry
    {
        public Guid Id { get; set; }
        public Guid OperationId { get; set; }
        public Guid TaskId { get; set; }

        public string Message { get; set; }
        public virtual OperationModel Operation { get; set; }
        public virtual OperationTaskModel Task { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}