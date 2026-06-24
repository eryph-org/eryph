using System;
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
