using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProviderRouterPortSpecs
{
    public sealed class GetForConfig : Specification<ProviderRouterPort>
    {
        public GetForConfig()
        {
            Query.Include(p => p.Network);
            Query.Include(p => p.IpAssignments)
                .ThenInclude(a => ((IpPoolAssignment)a).Pool);
            Query.Include(p => p.IpAssignments)
                .ThenInclude(a => a.Subnet);
        }
    }
}
