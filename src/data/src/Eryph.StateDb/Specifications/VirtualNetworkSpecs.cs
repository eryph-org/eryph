using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class VirtualNetworkSpecs
{
    public sealed class GetByName : Specification<VirtualNetwork>, ISingleResultSpecification
    {
        public GetByName(Guid projectId, string name)
        {
            Query.Where(x => x.Project.Id == projectId && x.Name == name);
        }

    }

    public sealed class GetForProjectConfig : Specification<VirtualNetwork>
    {
        public GetForProjectConfig(Guid projectId)
        {
            Query.Where(x => x.ProjectId == projectId)
                .Include(x => x.NetworkPorts)
                .Include(x => x.Subnets)
                .Include(x => x.RouterPort).ThenInclude(x => x.FloatingPort)
                .Include(x => x.Subnets)
                .ThenInclude(x => x.IpPools);

        }

    }
}
