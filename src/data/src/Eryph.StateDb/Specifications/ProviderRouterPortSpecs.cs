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
    public sealed class GetByNetworkId : Specification<ProviderRouterPort>, ISingleResultSpecification
    {
        public GetByNetworkId(Guid networkId)
        {
            Query.Where(x => x.NetworkId == networkId);
        }
    }
}
