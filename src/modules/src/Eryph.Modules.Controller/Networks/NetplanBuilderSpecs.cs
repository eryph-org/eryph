using System;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Networks;

public static class NetplanBuilderSpecs
{

    public sealed class GetAllProviderSubnets : Specification<ProviderSubnet>
    {
        public GetAllProviderSubnets()
        {
            Query
                .Include(x => x.IpPools);

        }
    }

    public sealed class GetAllNetworks : Specification<VirtualNetwork>
    {
        public GetAllNetworks(Guid projectId)
        {
            Query
                .Where(x => x.ProjectId == projectId)
                .Include(x=>x.RouterPort).ThenInclude(x=>x.IpAssignments)
                .Include(x => x.NetworkPorts)
                .ThenInclude(x => x.IpAssignments)
                .ThenInclude(x=>x.Subnet);

            Query.Include(x => x.NetworkPorts)
                .ThenInclude(x=>x.FloatingPort).ThenInclude(x=>x.IpAssignments)
                .Include(x => x.Subnets);

        }
    }
}