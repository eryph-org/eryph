
using Ardalis.Specification;
using Eryph.StateDb.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Specifications
{
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
}
