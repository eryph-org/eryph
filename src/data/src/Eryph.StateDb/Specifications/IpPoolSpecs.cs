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

    public sealed class GetByIp : Specification<IpPoolAssignment>, ISingleResultSpecification
    {
        public GetByIp(Guid poolId, string ipAddress)
        {
            Query.Where(x => x.PoolId == poolId && x.IpAddress == ipAddress);
        }
    }

}