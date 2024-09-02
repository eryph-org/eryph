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

    public sealed class GetAll : Specification<Gene>
    {
        public GetAll()
        {
        }
    }

    public sealed class GetUnused : Specification<Gene>
    {
        public GetUnused(string agentName, StateStoreContext context)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && !context.VirtualDisks.Any(d => d.StorageIdentifier == x.Name));
        }
    }

    public sealed class GetForInventory : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetForInventory(string agentName, GeneType geneType, GeneIdentifier geneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.GeneType == geneType
                             && x.GeneSet == geneId.GeneSet.Value
                             && x.Name == geneId.GeneName.Value);
        }
    }
}
