using System;
using System.Xml.Schema;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class VCatletSpecs
{
    public sealed class GetByVMId : Specification<VirtualCatlet>, ISingleResultSpecification
    {
        public GetByVMId(Guid vmId)
        {
            Query.Where(x => x.VMId == vmId)
                .Include(x => x.Project);
        }
    }

    public sealed class GetForConfig : Specification<VirtualCatlet>, ISingleResultSpecification
    {
        public GetForConfig(Guid catletId)
        {
            Query.Where(x => x.Id == catletId)
                .Include(x => x.Project)
                .Include(x => x.NetworkPorts)
                .ThenInclude(x => x.IpAssignments)
                .ThenInclude(x => x.Subnet)
                .Include(x => x.NetworkPorts)
                .ThenInclude(x => x.Network)

                .Include(x => x.Drives).ThenInclude(x => x.AttachedDisk);
        }
    }

}