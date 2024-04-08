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
    public sealed class GetForConfig : Specification<ProviderSubnet>
    {
        public GetForConfig()
        {
            Query.Include(s => s.IpPools);
        }
    }
}