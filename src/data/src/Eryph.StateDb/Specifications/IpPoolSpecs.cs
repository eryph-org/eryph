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


    public sealed class GetMinNumberStartingAt : Specification<IpPoolAssignment>, ISingleResultSpecification
    {
        public GetMinNumberStartingAt(Guid poolId, long startingAt)
        {
            Query.Where(x => x.PoolId == poolId && x.Number >= startingAt)
                .OrderBy(x => x.Number).Take(1);

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