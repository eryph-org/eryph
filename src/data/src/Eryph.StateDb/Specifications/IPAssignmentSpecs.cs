using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class IPAssignmentSpecs
{
    public sealed class GetByPort : Specification<IpAssignment>
    {
        public GetByPort(Guid portId)
        {
            Query.Where(x => x.NetworkPortId == portId);
        }
    }

    public sealed class GetByPortWithPoolAndSubnet : Specification<IpAssignment>
    {
        public GetByPortWithPoolAndSubnet(Guid portId)
        {
            Query.Where(x => x.NetworkPortId == portId)
                .Include(a => ((IpPoolAssignment)a).Pool)
                .Include(p => p.Subnet)
                .ThenInclude(s => ((VirtualNetworkSubnet)s!).Network);
        }
    }
}
