using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class IpPoolAssignmentSpecs
{
    public sealed class GetByNumber : Specification<IpPoolAssignment>, ISingleResultSpecification
    {
        public GetByNumber(Guid poolId, int number)
        {
            Query.Where(x => x.PoolId == poolId && x.Number == number);
        }
    }
}
