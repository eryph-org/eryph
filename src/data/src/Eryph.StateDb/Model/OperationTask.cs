using System;

namespace Eryph.StateDb.Model
{
    public class OperationTask
    {
        public Guid Id { get; set; }

        public virtual Operation Operation { get; set; }
        public OperationTaskStatus Status { get; set; }
        public string AgentName { get; set; }
        public string Name { get; set; }
    }
}