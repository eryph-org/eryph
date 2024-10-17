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
                             && x.GeneId == geneId.Value);
        }
    }

    public sealed class GetByUniqueGeneId : Specification<Gene>, ISingleResultSpecification<Gene>
    {
        public GetByUniqueGeneId(string agentName, UniqueGeneIdentifier geneId)
        {
            Query.Where(x => x.LastSeenAgent == agentName
                             && x.GeneId == geneId.Identifier.Value
                             && x.Architecture == geneId.Architecture.Value);
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

    public sealed class GetByUniqueGeneIds : Specification<Gene>
    {
        public GetByUniqueGeneIds(string agentName, IList<UniqueGeneIdentifier> geneIds)
        {
            var values = geneIds.Map(id => $"{id.Identifier.Value}|{id.Architecture.Value}").ToList();

            Query.Where(x => x.LastSeenAgent == agentName
                             && values.Contains(x.GeneId + "|" + x.Architecture));
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

    public sealed class GetMissing : Specification<Gene>
    {
        public GetMissing(string agentName, IList<GeneIdentifier> geneIds)
        {
            var values = geneIds.Map(id => id.Value).ToList();
            Query.Where(x => x.LastSeenAgent == agentName && !values.Contains(x.GeneId));
        }
    }
}
