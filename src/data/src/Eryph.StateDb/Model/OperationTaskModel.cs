using System;
using Eryph.StateDb.Workflows;

namespace Eryph.StateDb.Model
{
    public class OperationTaskModel
    {
        public Guid Id { get; set; }
        public Guid ParentTaskId { get; set; }

        public Guid OperationId { get; set; }
        
        public virtual OperationModel Operation { get; set; }
        public OperationTaskStatus Status { get; set; }
        public string AgentName { get; set; }
        public string Name { get; set; }
    }
}