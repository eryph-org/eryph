using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.StateDb.Model;
using System;
using LanguageExt;

namespace Eryph.StateDb.Specifications;

public sealed class CatletNetworkPortSpecs
{
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
                .ThenInclude(x => x!.IpAssignments);
            Query.Include(x => x.FloatingPort)
                .ThenInclude(x => x!.IpAssignments)
                .ThenInclude(x => x.Subnet);
            Query.Include(x => x.IpAssignments)
                .ThenInclude(x => ((IpPoolAssignment)x).Pool);
            Query.Include(x => x.IpAssignments)
                .ThenInclude(x => x.Subnet);
        }
    }

    public sealed class GetByName : Specification<CatletNetworkPort>, ISingleResultSpecification
    {
        public GetByName(Guid networkId, string name)
        {
            Query.Where(p => p.Network.Id == networkId && p.Name == name);
        }
    }

    public sealed class GetUnused : Specification<CatletNetworkPort>
    {
        public GetUnused(Guid catletMetadataId, Seq<string> usedPortNames)
        {
            var values = usedPortNames.ToArray();
            Query.Where(p => p.CatletMetadataId == catletMetadataId
                             && !values.Contains(p.Name))
                .Include(p => p.FloatingPort!);
        }
    }

    public sealed class GetByCatletMetadataIdAndName : Specification<CatletNetworkPort>, ISingleResultSpecification
    {
        public GetByCatletMetadataIdAndName(Guid catletMetadataId, string name)
        {
            Query.Where(p => p.CatletMetadataId == catletMetadataId && p.Name == name)
                .Include(p => p.FloatingPort!);
        }
    }
}
