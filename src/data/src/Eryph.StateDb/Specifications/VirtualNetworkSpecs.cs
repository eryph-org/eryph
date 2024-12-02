using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class VirtualNetworkSpecs
{
    public sealed class GetByName : Specification<VirtualNetwork>, ISingleResultSpecification
    {
        public GetByName(Guid projectId, string name, string environment)
        {
            Query.Where(x => x.Project.Id == projectId && x.Name == name && x.Environment == environment);
        }

    }

    public sealed class GetForProjectConfig : Specification<VirtualNetwork>
    {
        public GetForProjectConfig(Guid projectId)
        {
            Query.Where(x => x.ProjectId == projectId)
                .Include(x => x.NetworkPorts).ThenInclude(x => x.IpAssignments)
                .Include(x => x.Subnets)
                .Include(x => x.RouterPort).ThenInclude(x => x!.FloatingPort)
                .Include(x => x.Subnets)
                .ThenInclude(x => x.IpPools);
        }
    }

    public sealed class GetForNetworkSync : Specification<VirtualNetwork>
    {
        public GetForNetworkSync(Guid projectId)
        {
            Query.Where(x => x.ProjectId == projectId)
                .Include(x => x.NetworkPorts).ThenInclude(x => x.IpAssignments)
                .Include(x => x.NetworkPorts).ThenInclude(x => x.FloatingPort)
                .Include(x => x.Subnets)
                .Include(x => x.RouterPort).ThenInclude(x => x!.FloatingPort)
                .Include(x => x.Subnets)
                .ThenInclude(x => x.IpPools);
        }
    }

    public sealed class GetForChangeTracking : Specification<VirtualNetwork>
    {
        public GetForChangeTracking(Guid projectId)
        {
            Query.Where(x => x.ProjectId == projectId)
                .Include(x => x.NetworkPorts)
                .ThenInclude(p => p.FloatingPort)
                .Include(n => n.NetworkPorts)
                .ThenInclude(p => p.IpAssignments)
                .ThenInclude(a => ((IpPoolAssignment)a).Pool)
                .Include(n => n.NetworkPorts)
                .ThenInclude(p => p.IpAssignments)
                .ThenInclude(a => a.Subnet)
                .Include(x => x.Subnets)
                .Include(x => x.RouterPort)
                .ThenInclude(x => x!.FloatingPort)
                .Include(x => x.Subnets)
                .ThenInclude(x => x.IpPools);
        }
    }
}
