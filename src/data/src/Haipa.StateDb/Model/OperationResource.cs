using System;
using Haipa.Resources;

namespace Haipa.StateDb.Model
{
    public class OperationResource
    {
        public Guid Id { get; set; }
        public Guid ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }

        public Operation Operation { get; set; }
    }
}