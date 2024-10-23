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

    public sealed class GetByUniqueGeneId : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetByUniqueGeneId(string agentName, UniqueGeneIdentifier uniqueGeneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.UniqueGeneIndex == uniqueGeneId.ToUniqueGeneIndex());
        }
    }

    public sealed class GetByUniqueGeneIds : Specification<Gene>
    {
        public GetByUniqueGeneIds(string agentName, IList<UniqueGeneIdentifier> geneIds)
        {
            var values = geneIds.Map(id => id.ToUniqueGeneIndex()).ToList();
            Query.Where(x => x.LastSeenAgent == agentName && values.Contains(x.UniqueGeneIndex));
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
        public GetForInventory(string agentName, UniqueGeneIdentifier uniqueGeneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.GeneType == uniqueGeneId.GeneType
                             && x.UniqueGeneIndex == uniqueGeneId.ToUniqueGeneIndex());
        }
    }

    public sealed class GetMissing : Specification<Gene>
    {
        public GetMissing(string agentName, IList<UniqueGeneIdentifier> geneIds)
        {
            var values = geneIds.Map(id => id.ToUniqueGeneIndex()).ToList();
            Query.Where(x => x.LastSeenAgent == agentName && !values.Contains(x.UniqueGeneIndex));
        }
    }
}
