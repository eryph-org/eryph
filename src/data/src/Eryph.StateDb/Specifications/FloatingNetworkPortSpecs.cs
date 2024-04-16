using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public sealed class FloatingNetworkPortSpecs
{
    public sealed class GetForChangeTracking : Specification<FloatingNetworkPort>
    {
        public GetForChangeTracking()
        {
            Query.Include(p => p.IpAssignments)
                .ThenInclude(a => ((IpPoolAssignment)a).Pool);
            Query.Include(p => p.IpAssignments)
                .ThenInclude(a => a.Subnet);
        }
    }

    public sealed class GetByName : Specification<FloatingNetworkPort>, ISingleResultSpecification
    {
        public GetByName(string providerName, string subnetName, string name)
        {
            Query.Where(x => x.ProviderName == providerName
                             && x.SubnetName == subnetName
                             && x.Name == name);
        }
    }
}
