using System;

namespace VoxelWorldEngine.Registry
{
    public class ObjectKey : IComparable<ObjectKey>
    {
        public string Domain { get; }
        public string Name { get; }
        public int? InternalId { get; set; }

        public ObjectKey(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        public int CompareTo(ObjectKey other)
        {
            /*if(!InternalId.HasValue)
                throw new InvalidOperationException("The Object is not registered.");
            if (!other.InternalId.HasValue)
                throw new InvalidOperationException("The other Object is not registered.");
            return Math.Sign(InternalId.Value - other.InternalId.Value);*/
            int d = string.CompareOrdinal(Domain, other.Domain);
            if (d != 0)
                return d;

            return string.CompareOrdinal(Name, other.Name);
        }

        public override string ToString()
        {
            return $"{{{Domain}:{Name}}}";
        }
    }
}