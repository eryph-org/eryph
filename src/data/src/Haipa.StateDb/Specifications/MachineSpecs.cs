using Ardalis.Specification;
using Haipa.StateDb.Model;
using JetBrains.Annotations;

namespace Haipa.StateDb.Specifications
{
    public static class MachineSpecs<T> where T: Machine
    {

        public sealed class GetByName : Specification<T>
        {
            public GetByName(string name)
            {
                Query.Where(x => x.Name == name);
            }
        }

    }
}