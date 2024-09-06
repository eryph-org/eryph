using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public class GeneSpecs
{
    public sealed class GetById : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetById(Guid id)
        {
            Query.Where(x => x.Id == id);
        }
    }

    public sealed class GetByGeneId : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetByGeneId(string agentName, GeneIdentifier geneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.GeneId == geneId.GeneSet.Value);
        }
    }

    public sealed class GetByGeneIds : Specification<Gene>
    {
        public GetByGeneIds(string agentName, IList<GeneIdentifier> geneIds)
        {
            var values = geneIds.Map(id => id.Value).ToList();

            Query.Where(x => x.LastSeenAgent == agentName
                             && values.Contains(x.GeneId));
        }
    }

    public sealed class GetAll : Specification<Gene>
    {
        public GetAll()
        {
        }
    }

    public sealed class GetForInventory : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetForInventory(string agentName, GeneType geneType, GeneIdentifier geneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.GeneType == geneType
                             && x.GeneId == geneId.Value);
        }
    }

    public sealed class GetOutdated : Specification<Gene>
    {
        public GetOutdated(string agentName, DateTimeOffset timestamp)
        {
            Query.Where(x => x.LastSeenAgent == agentName && x.LastSeen < timestamp);
        }
    }
}
