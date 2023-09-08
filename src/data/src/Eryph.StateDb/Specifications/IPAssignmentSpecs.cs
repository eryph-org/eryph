using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class IPAssignmentSpecs
{
    public sealed class GetByPort : Specification<IpAssignment>, ISingleResultSpecification
    {
        public GetByPort(Guid portId)
        {
            Query.Where(x => x.NetworkPortId == portId);
        }
    }


}