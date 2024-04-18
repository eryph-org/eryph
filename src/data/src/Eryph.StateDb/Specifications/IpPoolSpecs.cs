using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class IpPoolSpecs
{
    public sealed class GetByName : Specification<IpPool>, ISingleResultSpecification
    {
        public GetByName(Guid subnetId, string poolName)
        {
            Query.Where(x => x.SubnetId == subnetId && x.Name == poolName);
        }
    }

    public sealed class GetAssignments : Specification<IpPoolAssignment>, ISingleResultSpecification
    {
        public GetAssignments(Guid poolId)
        {
            Query.Where(x => x.PoolId == poolId);
        }
    }
}
