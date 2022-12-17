using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class SubnetSpecs
{
    public sealed class GetByNetwork : Specification<VirtualNetworkSubnet>, ISingleResultSpecification
    {
        public GetByNetwork(Guid networkId, string subnetName)
        {
            Query.Where(x => x.NetworkId == networkId && x.Name == subnetName);
        }
    }


}