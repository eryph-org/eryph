using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class NetworkPortSpecs
{
    public sealed class GetById : Specification<VirtualNetworkPort>, ISingleResultSpecification
    {
        public GetById(Guid id)
        {
            Query.Where(x => x.Id == id);
        }

    }

    public sealed class GetByNetworkAndName : Specification<VirtualNetworkPort>, ISingleResultSpecification
    {
        public GetByNetworkAndName(Guid networkId, string name)
        {
            Query.Where( x => x.NetworkId == networkId && x.Name == name);
        }

    }

    // TODO fix my naming
    public sealed class GetByNetworkAndNameForCatlet : Specification<CatletNetworkPort>, ISingleResultSpecification
    {
        public GetByNetworkAndNameForCatlet(Guid networkId, string name)
        {
            Query.Where(x => x.NetworkId == networkId && x.Name == name);
        }

    }

}