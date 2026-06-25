using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Networks;

public static class NetplanBuilderSpecs
{
    public sealed class GetAllProviderSubnets : Specification<ProviderSubnet>
    {
        public GetAllProviderSubnets()
        {
            Query.Include(x => x.IpPools)
                .ThenInclude(p => p.IpAssignments);
        }
    }

    public sealed class GetAllNetworks : Specification<VirtualNetwork>
    {
        public GetAllNetworks(Guid projectId)
        {
            // EF Core Include/ThenInclude expressions over the optional RouterPort and
            // FloatingPort navigations are translated by EF and never dereferenced at
            // runtime, so the possible-null-dereference warning is a false positive here.
#pragma warning disable CS8602
            Query
                .Where(x => x.ProjectId == projectId)
                .Include(x => x.RouterPort).ThenInclude(x => x.IpAssignments)
                .Include(x => x.NetworkPorts)
                .ThenInclude(x => x.IpAssignments)
                .ThenInclude(x => x.Subnet);

            Query.Include(x => x.NetworkPorts)
                .ThenInclude(x => x.FloatingPort).ThenInclude(x => x.IpAssignments)
                .Include(x => x.Subnets);
#pragma warning restore CS8602
        }
    }
}
