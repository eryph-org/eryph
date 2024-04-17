using System;
using System.Collections.Generic;
using Eryph.Resources;
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
        public string DisplayName { get; set; }

        
        public TaskReferenceType? ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string ReferenceProjectName { get; set; }

        public DateTimeOffset LastUpdated { get; set; }
        public virtual List<TaskProgressEntry> Progress { get; set; }
    }
}