using System;
using Eryph.Resources;

namespace Eryph.StateDb.Model
{
    public class OperationResource
    {
        public Guid Id { get; set; }
        public Guid ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }

        public virtual Operation Operation { get; set; }
    }

    public class OperationProject
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project{ get; set; }

        public virtual Operation Operation { get; set; }
    }
}