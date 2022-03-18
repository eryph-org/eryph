using System;

namespace Eryph.Resources
{
    public struct Resource : IComparable<Resource>
    {
        public Guid Id { get; set; }
        public ResourceType Type { get; set; }

        public Resource(ResourceType resourceType, Guid resourceId)
        {
            Type = resourceType;
            Id = resourceId;
        }

        public int CompareTo(Resource other)
        {
            var idComparison = Id.CompareTo(other.Id);
            return idComparison != 0 ? idComparison : Type.CompareTo(other.Type);
        }
    }
}