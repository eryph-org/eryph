using Ardalis.Specification;
using Eryph.StateDb.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.StateDb.Specifications
{
    public sealed class CatletNetworkPortSpecs
    {
        public sealed class GetForProjectConfig : Specification<CatletNetworkPort>
        {
            public GetForProjectConfig(Guid projectId)
            {
                Query.Where(p => p.Network.ProjectId == projectId);
                
                Query.Include(p => p.Network);
                Query.Include(p => p.FloatingPort);
                Query.Include(p => p.IpAssignments)
                    .ThenInclude(a => ((IpPoolAssignment)a).Pool);
                Query.Include(p => p.IpAssignments)
                    .ThenInclude(a => a.Subnet);
            }
        }

        public sealed class GetByCatletMetadataId : Specification<CatletNetworkPort>
        {
            public GetByCatletMetadataId(Guid metadataId)
            {
                Query.Where(x => x.CatletMetadataId == metadataId);
                
                Query.Include(x => x.Network)
                    .ThenInclude(x => x.Subnets)
                    .ThenInclude(x => x.IpPools);
                Query.Include(x => x.Network)
                    .ThenInclude(x => x.RouterPort)
                    .ThenInclude(x => x.IpAssignments);
                Query.Include(x => x.FloatingPort)
                    .ThenInclude(x => x.IpAssignments)
                    .ThenInclude(x => x.Subnet);
                Query.Include(x => x.IpAssignments)
                    .ThenInclude(x => ((IpPoolAssignment)x).Pool);
                Query.Include(x => x.IpAssignments)
                    .ThenInclude(x => x.Subnet);
            }
        }
    }
}
