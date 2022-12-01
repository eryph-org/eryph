using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class VirtualNetworkSpecs
{
    public sealed class GetByName : Specification<VirtualNetwork>, ISingleResultSpecification
    {
        public GetByName(Guid projectId, string name)
        {
            Query.Where(x => x.Project.Id == projectId && x.Name == name);
        }

    }


}
