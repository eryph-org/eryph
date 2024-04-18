using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProviderSubnetSpecs
{
    public sealed class GetForChangeTracking : Specification<ProviderSubnet>
    {
        public GetForChangeTracking()
        {
            Query.Include(s => s.IpPools);
        }
    }
}
