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
                Query.Where(x => x.Network.ProjectId == projectId);
                Query.Include(x => x.Network);
                Query.Include(x => x.FloatingPort);
                Query.Include(x => x.IpAssignments)
                    .ThenInclude(x => ((IpPoolAssignment)x).Pool);
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
                Query.Include(x => x.FloatingPort);
                Query.Include(x => x.IpAssignments)
                    .ThenInclude(x => ((IpPoolAssignment)x).Pool);
            }
        }
    }
}
