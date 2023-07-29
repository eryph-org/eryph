using System;

namespace Eryph.Resources
{
    public struct Resource : IComparable<Resource>, IEquatable<Resource>
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

        public bool Equals(Resource other)
        {
            return Id.Equals(other.Id) && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            return obj is Resource other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, (int)Type);
        }

        public static bool operator ==(Resource left, Resource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Resource left, Resource right)
        {
            return !(left == right);
        }

        public static bool operator <(Resource left, Resource right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(Resource left, Resource right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(Resource left, Resource right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(Resource left, Resource right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}