using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.ComputeApi.Model
{
    public class CatletSpecBuilder : ResourceSpecBuilder<Catlet>
    {
        public CatletSpecBuilder(IUserRightsProvider userRightsProvider) : base(userRightsProvider)
        {
        }

        protected override void CustomizeQuery(ISpecificationBuilder<Catlet> specification)
        {
            specification.Include(x => x.ReportedNetworks);
            specification.Include(x => x.NetworkPorts)
                .ThenInclude(x => x.Network)
                .ThenInclude(x => x.RouterPort).ThenInclude(x => x.IpAssignments);

            specification.Include(x => x.NetworkPorts).ThenInclude(x=>x.IpAssignments)
                .ThenInclude(x=>x.Subnet);
            specification.Include(x => x.NetworkPorts)
                .ThenInclude(x => x.FloatingPort).ThenInclude(x => x.IpAssignments)
                .ThenInclude(x => x.Subnet);
        }
    }
}