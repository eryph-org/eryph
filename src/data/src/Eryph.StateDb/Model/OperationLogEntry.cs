using System;

namespace Eryph.StateDb.Model
{
    public class OperationLogEntry
    {
        public Guid Id { get; set; }

        public string Message { get; set; }
        public Operation Operation { get; set; }
        public OperationTask Task { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}