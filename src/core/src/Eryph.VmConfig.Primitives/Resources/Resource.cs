using System;

namespace Eryph.Resources
{
    public struct Resource
    {
        public Guid Id { get; set; }
        public ResourceType Type { get; set; }

        public Resource(ResourceType resourceType, Guid resourceId)
        {
            Type = resourceType;
            Id = resourceId;
        }
    }
}