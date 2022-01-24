using System;
using Eryph.Resources;

namespace Eryph.StateDb.Model
{
    public class OperationResource
    {
        public Guid Id { get; set; }
        public Guid ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }

        public Operation Operation { get; set; }
    }
}