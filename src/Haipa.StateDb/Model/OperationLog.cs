using System;

namespace Haipa.StateDb.Model
{
    public class OperationLog
    {
        public Guid Id { get; set; }
        
        public string Message { get; set; }
        public Operation Operation { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}